using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DIAN.Admin.Web
{
    /// <summary>Resultado de comprobar el código del segundo factor.</summary>
    public enum ResultadoSegundoFactor
    {
        Correcto,
        Incorrecto,

        /// <summary>El código caducó, el ticket no existe o ya se usó. Hay que volver a empezar el ingreso.</summary>
        Caducado,

        /// <summary>Se agotaron los intentos de ese código.</summary>
        Agotado
    }

    /// <summary>
    /// Segundo factor del operador: un código de seis dígitos enviado por WhatsApp al celular registrado del usuario.
    /// Los códigos viven en memoria del proceso (no en disco) y caducan a los cinco minutos; reiniciar el servicio los
    /// anula a todos, que es lo deseable. Un solo proceso atiende el panel, así que no hace falta compartirlos.
    /// </summary>
    public sealed class SegundoFactor
    {
        public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);
        private const int IntentosMaximos = 3;

        private sealed class Pendiente
        {
            public string Usuario { get; init; } = string.Empty;
            public string Codigo { get; init; } = string.Empty;
            public DateTime CaducaUtc { get; init; }
            public int Intentos;
        }

        private readonly ConcurrentDictionary<string, Pendiente> _pendientes = new ConcurrentDictionary<string, Pendiente>(StringComparer.Ordinal);
        private readonly ICanalWhatsApp _whatsapp;

        public SegundoFactor(ICanalWhatsApp whatsapp)
        {
            _whatsapp = whatsapp;
        }

        /// <summary>
        /// Genera un código, lo envía al celular y devuelve el ticket que identifica el intento (va en una cookie de
        /// corta duración). Devuelve null si el gateway no aceptó el mensaje: entonces no se deja entrar.
        /// </summary>
        public async Task<string?> IniciarAsync(string usuario, string telefono)
        {
            LimpiarCaducados();

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var mensaje = "Su codigo para entrar al panel DIAN es " + codigo
                          + ". Vence en " + (int)Vigencia.TotalMinutes + " minutos. Si no fue usted, avise a soporte y cambie su contrasena.";

            if (!await _whatsapp.EnviarAsync(telefono, mensaje))
            {
                return null;
            }

            // Un intento nuevo invalida el anterior del mismo usuario: así un código viejo no sirve de repuesto.
            foreach (var par in _pendientes.Where(p => string.Equals(p.Value.Usuario, usuario, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _pendientes.TryRemove(par.Key, out _);
            }

            var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _pendientes[ticket] = new Pendiente
            {
                Usuario = usuario,
                Codigo = codigo,
                CaducaUtc = DateTime.UtcNow.Add(Vigencia)
            };

            return ticket;
        }

        /// <summary>Usuario al que pertenece un ticket vigente, o null.</summary>
        public string? UsuarioDe(string? ticket)
        {
            if (string.IsNullOrEmpty(ticket) || !_pendientes.TryGetValue(ticket, out var pendiente))
            {
                return null;
            }

            return pendiente.CaducaUtc > DateTime.UtcNow ? pendiente.Usuario : null;
        }

        /// <summary>Comprueba el código. Un acierto consume el ticket; tres fallos también.</summary>
        public ResultadoSegundoFactor Verificar(string? ticket, string? codigo)
        {
            if (string.IsNullOrEmpty(ticket) || !_pendientes.TryGetValue(ticket, out var pendiente))
            {
                return ResultadoSegundoFactor.Caducado;
            }

            if (pendiente.CaducaUtc <= DateTime.UtcNow)
            {
                _pendientes.TryRemove(ticket, out _);
                return ResultadoSegundoFactor.Caducado;
            }

            var limpio = new string((codigo ?? string.Empty).Where(char.IsDigit).ToArray());
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(pendiente.Codigo), Encoding.ASCII.GetBytes(limpio.PadRight(6).Substring(0, 6))))
            {
                _pendientes.TryRemove(ticket, out _);
                return ResultadoSegundoFactor.Correcto;
            }

            if (System.Threading.Interlocked.Increment(ref pendiente.Intentos) >= IntentosMaximos)
            {
                _pendientes.TryRemove(ticket, out _);
                return ResultadoSegundoFactor.Agotado;
            }

            return ResultadoSegundoFactor.Incorrecto;
        }

        /// <summary>Descarta el intento en curso (el usuario volvió al ingreso).</summary>
        public void Cancelar(string? ticket)
        {
            if (!string.IsNullOrEmpty(ticket))
            {
                _pendientes.TryRemove(ticket, out _);
            }
        }

        private void LimpiarCaducados()
        {
            foreach (var par in _pendientes.Where(p => p.Value.CaducaUtc <= DateTime.UtcNow).ToList())
            {
                _pendientes.TryRemove(par.Key, out _);
            }
        }
    }
}
