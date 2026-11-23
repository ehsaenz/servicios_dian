using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DIAN.Admin.Web
{
    /// <summary>
    /// Administradores de la app. Contraseñas con PBKDF2-SHA256 y sal propia; tras 5 intentos fallidos el usuario
    /// se bloquea 15 minutos. Nunca se guarda ni se registra la contraseña.
    /// </summary>
    public sealed class AdminUsuarios
    {
        public const int ClaveMinima = 12;
        private const int Iteraciones = 210_000;
        private const int IntentosMaximos = 5;
        private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions { WriteIndented = true };

        private sealed class Registro
        {
            public string Usuario { get; set; } = string.Empty;
            public string Sal { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;

            /// <summary>Id del cliente al que pertenece el usuario; vacío = operador de la plataforma.</summary>
            public string? ClienteId { get; set; }

            /// <summary>Celular para el segundo factor por WhatsApp, con indicativo de país. Solo lo usan los operadores.</summary>
            public string? Telefono { get; set; }
            public int Fallos { get; set; }
            public DateTime? BloqueadoHastaUtc { get; set; }
        }

        private readonly string _archivo;
        private readonly object _candado = new object();

        public AdminUsuarios(string raiz)
        {
            Directory.CreateDirectory(raiz);
            _archivo = Path.Combine(raiz, "admin-usuarios.json");
        }

        /// <summary>Crea (o reemplaza) un usuario. Con <paramref name="clienteId"/> es un usuario del portal de ese cliente.</summary>
        public void Crear(string usuario, string clave, string? clienteId = null)
        {
            if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 40 || usuario.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("El usuario no puede estar vacío, tener espacios ni pasar de 40 caracteres.");
            }

            if (clave == null || clave.Length < ClaveMinima)
            {
                throw new ArgumentException($"La contraseña debe tener al menos {ClaveMinima} caracteres.");
            }

            lock (_candado)
            {
                var lista = Leer();
                var previo = lista.FirstOrDefault(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
                if (previo != null && !string.Equals(previo.ClienteId ?? string.Empty, clienteId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Ese nombre de usuario ya existe en otro ámbito.");
                }

                lista.RemoveAll(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
                var sal = RandomNumberGenerator.GetBytes(16);
                lista.Add(new Registro
                {
                    Usuario = usuario,
                    Sal = Convert.ToBase64String(sal),
                    Hash = Convert.ToBase64String(Derivar(clave, sal)),
                    ClienteId = string.IsNullOrWhiteSpace(clienteId) ? null : clienteId,

                    // `crear-admin` tambien sirve para restablecer la contraseña de un operador. Si se perdiera el
                    // celular, quien la restablece se quedaria sin segundo factor y sin entrada publica, justo cuando
                    // mas falta le hace entrar.
                    Telefono = previo?.Telefono
                });
                Escribir(lista);
            }
        }

        /// <summary>Id del cliente del usuario, o null si es operador (o no existe).</summary>
        public string? ClienteDe(string usuario)
        {
            lock (_candado)
            {
                return Leer().FirstOrDefault(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase))?.ClienteId;
            }
        }

        /// <summary>Celular del segundo factor, o null si el usuario no tiene uno (o no existe).</summary>
        public string? TelefonoDe(string usuario)
        {
            lock (_candado)
            {
                var telefono = Leer().FirstOrDefault(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase))?.Telefono;
                return string.IsNullOrWhiteSpace(telefono) ? null : telefono;
            }
        }

        /// <summary>
        /// Fija (o quita, con <paramref name="telefono"/> vacío) el celular del segundo factor. Devuelve false si el usuario
        /// no existe. El número se guarda normalizado: solo dígitos, con indicativo 57 cuando vienen diez.
        /// </summary>
        public bool FijarTelefono(string usuario, string? telefono)
        {
            var normalizado = NormalizarTelefono(telefono);
            if (telefono != null && !string.IsNullOrWhiteSpace(telefono) && normalizado == null)
            {
                throw new ArgumentException("El teléfono debe tener entre 10 y 15 dígitos.");
            }

            lock (_candado)
            {
                var lista = Leer();
                var registro = lista.FirstOrDefault(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
                if (registro == null)
                {
                    return false;
                }

                registro.Telefono = normalizado;
                Escribir(lista);
                return true;
            }
        }

        /// <summary>
        /// Deja solo los dígitos y antepone el 57 cuando son exactamente diez (celular colombiano). Devuelve null si el
        /// resultado no parece un número. Misma regla que el resto de los envíos por WhatsApp del grupo.
        /// </summary>
        public static string? NormalizarTelefono(string? telefono)
        {
            var digitos = new string((telefono ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digitos.Length == 10)
            {
                digitos = "57" + digitos;
            }

            return digitos.Length is >= 10 and <= 15 ? digitos : null;
        }

        /// <summary>Últimos cuatro dígitos, para decir a dónde se envió el código sin mostrar el número entero.</summary>
        public static string Enmascarar(string telefono) =>
            telefono.Length <= 4 ? telefono : "···" + telefono.Substring(telefono.Length - 4);

        /// <summary>Nombres de los usuarios del portal de un cliente.</summary>
        public IReadOnlyList<string> DeCliente(string clienteId)
        {
            lock (_candado)
            {
                return Leer().Where(r => string.Equals(r.ClienteId, clienteId, StringComparison.OrdinalIgnoreCase)).Select(r => r.Usuario).OrderBy(u => u).ToList();
            }
        }

        /// <summary>Elimina un usuario de un cliente (nunca un operador). Devuelve false si no existía.</summary>
        public bool EliminarDeCliente(string clienteId, string usuario)
        {
            lock (_candado)
            {
                var lista = Leer();
                var quitados = lista.RemoveAll(r => string.Equals(r.ClienteId, clienteId, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
                if (quitados > 0)
                {
                    Escribir(lista);
                }

                return quitados > 0;
            }
        }

        /// <summary>Cambia la contraseña de un usuario que ya existe, comprobando la actual.</summary>
        public async Task<bool> CambiarClaveAsync(string usuario, string actual, string nueva)
        {
            if (nueva == null || nueva.Length < ClaveMinima)
            {
                throw new ArgumentException($"La contraseña debe tener al menos {ClaveMinima} caracteres.");
            }

            if (!await ValidarAsync(usuario, actual))
            {
                return false;
            }

            lock (_candado)
            {
                var lista = Leer();
                var registro = lista.First(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
                var sal = RandomNumberGenerator.GetBytes(16);
                registro.Sal = Convert.ToBase64String(sal);
                registro.Hash = Convert.ToBase64String(Derivar(nueva, sal));
                Escribir(lista);
            }

            return true;
        }

        /// <summary>Genera una contraseña aleatoria legible para entregarla una sola vez.</summary>
        public static string GenerarClave()
        {
            const string alfabeto = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[16];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
            }

            return new string(chars);
        }

        public bool HayUsuarios()
        {
            lock (_candado)
            {
                return Leer().Count > 0;
            }
        }

        /// <summary>True si el usuario y la contraseña son correctos y el usuario no está bloqueado.</summary>
        public Task<bool> ValidarAsync(string usuario, string clave)
        {
            lock (_candado)
            {
                var lista = Leer();
                var registro = lista.FirstOrDefault(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));

                if (registro == null)
                {
                    // Mismo costo que con un usuario real, para no revelar cuáles existen.
                    Derivar(clave ?? string.Empty, new byte[16]);
                    return Task.FromResult(false);
                }

                if (registro.BloqueadoHastaUtc is { } hasta && hasta > DateTime.UtcNow)
                {
                    return Task.FromResult(false);
                }

                var esperado = Convert.FromBase64String(registro.Hash);
                var real = Derivar(clave ?? string.Empty, Convert.FromBase64String(registro.Sal));
                if (CryptographicOperations.FixedTimeEquals(esperado, real))
                {
                    registro.Fallos = 0;
                    registro.BloqueadoHastaUtc = null;
                    Escribir(lista);
                    return Task.FromResult(true);
                }

                registro.Fallos++;
                if (registro.Fallos >= IntentosMaximos)
                {
                    registro.BloqueadoHastaUtc = DateTime.UtcNow.Add(Bloqueo);
                    registro.Fallos = 0;
                }

                Escribir(lista);
                return Task.FromResult(false);
            }
        }

        public bool EstaBloqueado(string usuario)
        {
            lock (_candado)
            {
                var registro = Leer().FirstOrDefault(r => string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
                return registro?.BloqueadoHastaUtc is { } hasta && hasta > DateTime.UtcNow;
            }
        }

        private static byte[] Derivar(string clave, byte[] sal) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(clave), sal, Iteraciones, HashAlgorithmName.SHA256, 32);

        private List<Registro> Leer() =>
            File.Exists(_archivo) ? JsonSerializer.Deserialize<List<Registro>>(File.ReadAllText(_archivo)) ?? new List<Registro>() : new List<Registro>();

        private void Escribir(List<Registro> lista)
        {
            var temporal = _archivo + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(lista, Opciones));
            File.Move(temporal, _archivo, true);
        }
    }
}
