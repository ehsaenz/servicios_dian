using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Tenants
{
    public sealed class ResultadoCertificado
    {
        public string Sujeto { get; set; } = string.Empty;
        public DateTime VenceUtc { get; set; }
        public List<string> Advertencias { get; } = new List<string>();
    }

    /// <summary>Errores que la app Admin puede mostrar tal cual al administrador.</summary>
    public sealed class ErrorAdministracion : Exception
    {
        public ErrorAdministracion(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// Todas las operaciones de alta y mantenimiento de un cliente. La app Admin solo llama aquí, de modo que las reglas
    /// (validar el certificado, proteger secretos, auditar) están en un solo lugar y se pueden probar sin la web.
    /// </summary>
    public sealed class ClienteAdminServicio
    {
        public const string TextoAutorizacionProduccion = "AUTORIZO PRODUCCION";
        private const int MaximoLogoBytes = 1024 * 1024;
        private static readonly string[] ExtensionesLogo = { ".png", ".jpg", ".jpeg" };

        private readonly ITenantStore _almacen;
        private readonly ISecretProtector _protector;
        private readonly Auditoria _auditoria;

        public ClienteAdminServicio(ITenantStore almacen, ISecretProtector protector, Auditoria auditoria)
        {
            _almacen = almacen;
            _protector = protector;
            _auditoria = auditoria;
        }

        public Task<IReadOnlyList<ClienteRegistro>> ListarAsync(CancellationToken c = default) => _almacen.ListarAsync(c);

        public Task<ClienteRegistro?> ObtenerAsync(string id, CancellationToken c = default) => _almacen.ObtenerAsync(id, c);

        public async Task<ClienteRegistro> CrearAsync(string usuario, string id, EmisorConfig emisor, CancellationToken c = default)
        {
            if (!ClienteRegistro.EsIdValido(id))
            {
                throw new ErrorAdministracion("El id solo admite letras, números, guion y guion bajo (1 a 40 caracteres).");
            }

            if (await _almacen.ObtenerAsync(id, c) != null)
            {
                throw new ErrorAdministracion($"Ya existe un cliente con el id {id}.");
            }

            ValidarEmisor(emisor);
            var cliente = new ClienteRegistro { Id = id, Emisor = emisor };
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "crear", id, $"NIT {emisor.Nit}");
            return cliente;
        }

        public async Task ActualizarEmisorAsync(string usuario, string id, EmisorConfig emisor, string? perfilCorreo, CancellationToken c = default)
        {
            ValidarEmisor(emisor);
            var cliente = await Requerir(id, c);
            cliente.Emisor = emisor;
            cliente.PerfilCorreo = string.IsNullOrWhiteSpace(perfilCorreo) ? null : perfilCorreo.Trim();
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "emisor", id, $"NIT {emisor.Nit}");
        }

        /// <summary>Guarda el software de un tipo de documento. Si <paramref name="pin"/> viene vacío se conserva el anterior.</summary>
        public async Task GuardarSoftwareAsync(string usuario, string id, TipoSoftware tipo, string identificador, string? pin,
            bool ambientePruebas, string? testSetId, CancellationToken c = default)
        {
            if (tipo != TipoSoftware.FacturacionElectronica && tipo != TipoSoftware.DocumentoSoporte && tipo != TipoSoftware.DocumentoEquivalente
                && tipo != TipoSoftware.NominaElectronica && tipo != TipoSoftware.EventosRadian)
            {
                throw new ErrorAdministracion("Esta API atiende facturación electrónica (factura y notas), documento soporte, documento equivalente POS, nómina electrónica y eventos RADIAN.");
            }

            if (!Guid.TryParse(identificador, out _))
            {
                throw new ErrorAdministracion("El identificador del software debe ser un GUID, como el que entrega la DIAN.");
            }

            var cliente = await Requerir(id, c);
            var actual = cliente.Software.FirstOrDefault(s => s.TipoSoftware == tipo);
            var pinProtegido = !string.IsNullOrWhiteSpace(pin) ? _protector.Proteger(pin.Trim()) : actual?.PinProtegido ?? string.Empty;
            if (string.IsNullOrEmpty(pinProtegido))
            {
                throw new ErrorAdministracion("El PIN del software es obligatorio.");
            }

            cliente.Software.RemoveAll(s => s.TipoSoftware == tipo);
            cliente.Software.Add(new SoftwareRegistro
            {
                TipoSoftware = tipo,
                Identificador = identificador.Trim(),
                PinProtegido = pinProtegido,
                AmbientePruebas = ambientePruebas,
                TestSetId = (testSetId ?? string.Empty).Trim()
            });
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "software", id, $"{tipo}, pruebas={ambientePruebas}" + (string.IsNullOrWhiteSpace(pin) ? "" : ", PIN nuevo"));
        }

        /// <summary>Valida el .p12 con su clave y lo guarda. Nada se guarda si el certificado no abre, está vencido o no tiene llave privada.</summary>
        public async Task<ResultadoCertificado> SubirCertificadoAsync(string usuario, string id, byte[] p12, string clave, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            var resultado = ValidarCertificado(p12, clave, cliente.Emisor.Nit);

            await _almacen.GuardarArchivoAsync(id, "certificado.p12", p12, c);
            cliente.ArchivoCertificado = "certificado.p12";
            cliente.ClaveCertificadoProtegida = _protector.Proteger(clave);
            cliente.CertificadoSujeto = resultado.Sujeto;
            cliente.CertificadoVenceUtc = resultado.VenceUtc;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "certificado", id, $"{resultado.Sujeto}, vence {resultado.VenceUtc:yyyy-MM-dd}");
            return resultado;
        }

        public static ResultadoCertificado ValidarCertificado(byte[] p12, string clave, string nit)
        {
            X509Certificate2 cert;
            try
            {
                cert = X509CertificateLoader.LoadPkcs12(p12, clave, X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException)
            {
                throw new ErrorAdministracion("No se pudo abrir el certificado: el archivo no es un .p12 válido o la clave es incorrecta.");
            }

            using (cert)
            {
                if (!cert.HasPrivateKey)
                {
                    throw new ErrorAdministracion("El certificado no trae la llave privada, y sin ella no se puede firmar.");
                }

                var vence = cert.NotAfter.ToUniversalTime();
                if (vence <= DateTime.UtcNow)
                {
                    throw new ErrorAdministracion($"El certificado está vencido desde {vence:yyyy-MM-dd}.");
                }

                var resultado = new ResultadoCertificado { Sujeto = cert.Subject, VenceUtc = vence };
                if (!string.IsNullOrWhiteSpace(nit) && !cert.Subject.Contains(nit) && !cert.Issuer.Contains(nit))
                {
                    resultado.Advertencias.Add($"El NIT {nit} no aparece en el sujeto del certificado; verifique que sea el del cliente.");
                }

                if (vence < DateTime.UtcNow.AddDays(30))
                {
                    resultado.Advertencias.Add($"El certificado vence pronto ({vence:yyyy-MM-dd}).");
                }

                return resultado;
            }
        }

        /// <summary>Valida que el archivo sea un logo aceptable y devuelve su extensión (.png o .jpg).</summary>
        private static string ValidarLogo(string nombreOriginal, byte[] contenido)
        {
            var extension = Path.GetExtension(nombreOriginal ?? string.Empty).ToLowerInvariant();
            if (!ExtensionesLogo.Contains(extension))
            {
                throw new ErrorAdministracion("El logo debe ser una imagen PNG o JPG.");
            }

            if (contenido.Length == 0 || contenido.Length > MaximoLogoBytes)
            {
                throw new ErrorAdministracion("El logo debe pesar entre 1 byte y 1 MB.");
            }

            if (!TieneFirmaDeImagen(contenido))
            {
                throw new ErrorAdministracion("El archivo no es una imagen PNG o JPG válida.");
            }

            return extension == ".png" ? ".png" : ".jpg";
        }

        /// <summary>Crea o actualiza una sucursal (perfil de impresión). Conserva el logo que ya tenga.</summary>
        public async Task GuardarSucursalAsync(string usuario, string id, string matricula, string nombre, string? regimenFiscal, CancellationToken c = default)
        {
            matricula = (matricula ?? string.Empty).Trim();
            if (!SucursalRegistro.EsMatriculaValida(matricula))
            {
                throw new ErrorAdministracion("La matrícula mercantil solo admite letras, números, punto, guion y guion bajo (1 a 30 caracteres).");
            }

            nombre = (nombre ?? string.Empty).Trim();
            if (nombre.Length == 0 || nombre.Length > 200)
            {
                throw new ErrorAdministracion("El nombre de la sucursal es obligatorio (hasta 200 caracteres).");
            }

            regimenFiscal = (regimenFiscal ?? string.Empty).Trim();
            if (regimenFiscal.Length > 100)
            {
                throw new ErrorAdministracion("El régimen fiscal admite hasta 100 caracteres.");
            }

            var cliente = await Requerir(id, c);
            var sucursal = cliente.Sucursales.FirstOrDefault(s => string.Equals(s.MatriculaMercantil, matricula, StringComparison.OrdinalIgnoreCase));
            if (sucursal == null)
            {
                sucursal = new SucursalRegistro { MatriculaMercantil = matricula };
                cliente.Sucursales.Add(sucursal);
            }

            sucursal.Nombre = nombre;
            sucursal.RegimenFiscal = regimenFiscal;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "sucursal", id, $"{matricula}: {nombre}");
        }

        public async Task SubirLogoSucursalAsync(string usuario, string id, string matricula, string nombreOriginal, byte[] contenido, CancellationToken c = default)
        {
            var extension = ValidarLogo(nombreOriginal, contenido);
            contenido = PrepararLogo(extension, contenido, out var quitados);
            var cliente = await Requerir(id, c);
            var sucursal = cliente.Sucursales.FirstOrDefault(s => string.Equals(s.MatriculaMercantil, matricula, StringComparison.OrdinalIgnoreCase))
                ?? throw new ErrorAdministracion($"No existe la sucursal {matricula}.");

            // La matrícula solo tiene caracteres seguros para un nombre de archivo (ver EsMatriculaValida).
            var nombre = "logo-" + sucursal.MatriculaMercantil + extension;
            if (!string.IsNullOrEmpty(sucursal.ArchivoLogo) && sucursal.ArchivoLogo != nombre)
            {
                await _almacen.EliminarArchivoAsync(id, sucursal.ArchivoLogo, c);
            }

            await _almacen.GuardarArchivoAsync(id, nombre, contenido, c);
            sucursal.ArchivoLogo = nombre;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "sucursal-logo", id, $"{sucursal.MatriculaMercantil}: {nombre}{DescribirQuitados(quitados)}");
        }

        /// <summary>
        /// Configura el webhook del cliente (una URL fija). Devuelve el secreto de firma cuando es nuevo (primera vez o
        /// regenerado): es la única vez que se muestra. Con la URL vacía se quita el webhook y el ERP vuelve al polling.
        /// </summary>
        public async Task<string?> ConfigurarWebhookAsync(string usuario, string id, string? url, bool regenerarSecreto, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            url = (url ?? string.Empty).Trim();

            if (url.Length == 0)
            {
                cliente.WebhookUrl = null;
                cliente.WebhookSecretoProtegido = null;
                await _almacen.GuardarAsync(cliente, c);
                _auditoria.Registrar(usuario, "webhook-quitar", id, string.Empty);
                return null;
            }

            if (url.Length > 300 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ErrorAdministracion("La URL del webhook debe ser http o https, sin usuario ni clave, de hasta 300 caracteres.");
            }

            string? secretoNuevo = null;
            if (regenerarSecreto || string.IsNullOrEmpty(cliente.WebhookSecretoProtegido))
            {
                secretoNuevo = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                cliente.WebhookSecretoProtegido = _protector.Proteger(secretoNuevo);
            }

            cliente.WebhookUrl = uri.AbsoluteUri;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "webhook", id, uri.Host + (secretoNuevo != null ? ", secreto nuevo" : string.Empty));
            return secretoNuevo;
        }

        public async Task EliminarSucursalAsync(string usuario, string id, string matricula, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            var sucursal = cliente.Sucursales.FirstOrDefault(s => string.Equals(s.MatriculaMercantil, matricula, StringComparison.OrdinalIgnoreCase))
                ?? throw new ErrorAdministracion($"No existe la sucursal {matricula}.");

            cliente.Sucursales.Remove(sucursal);
            await _almacen.GuardarAsync(cliente, c);
            if (!string.IsNullOrEmpty(sucursal.ArchivoLogo))
            {
                await _almacen.EliminarArchivoAsync(id, sucursal.ArchivoLogo, c);
            }

            _auditoria.Registrar(usuario, "sucursal-eliminar", id, sucursal.MatriculaMercantil);
        }

        public async Task SubirLogoAsync(string usuario, string id, string nombreOriginal, byte[] contenido, CancellationToken c = default)
        {
            var formato = ValidarLogo(nombreOriginal, contenido);
            contenido = PrepararLogo(formato, contenido, out var quitados);
            var extension = Path.GetExtension(nombreOriginal).ToLowerInvariant();
            var cliente = await Requerir(id, c);
            var nombre = "logo" + (extension == ".png" ? ".png" : ".jpg");
            if (!string.IsNullOrEmpty(cliente.ArchivoLogo) && cliente.ArchivoLogo != nombre)
            {
                await _almacen.EliminarArchivoAsync(id, cliente.ArchivoLogo, c);
            }

            await _almacen.GuardarArchivoAsync(id, nombre, contenido, c);
            cliente.ArchivoLogo = nombre;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "logo", id, nombre + DescribirQuitados(quitados));
        }

        private static bool TieneFirmaDeImagen(byte[] b)
        {
            var png = b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
            var jpg = b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
            return png || jpg;
        }

        /// <summary>
        /// Quita del logo los metadatos que DevExpress no puede leer (ver <see cref="LimpiadorLogo"/>), en PNG y en JPG.
        /// </summary>
        private static byte[] PrepararLogo(string formato, byte[] contenido, out List<string> quitados)
        {
            try
            {
                return formato == ".png"
                    ? LimpiadorLogo.LimpiarPng(contenido, out quitados)
                    : LimpiadorLogo.LimpiarJpeg(contenido, out quitados);
            }
            catch (InvalidDataException)
            {
                var tipo = formato == ".png" ? "PNG" : "JPG";
                throw new ErrorAdministracion($"El {tipo} está dañado o no se puede leer. Expórtelo de nuevo como {tipo} y vuelva a subirlo.");
            }
        }

        private static string DescribirQuitados(List<string> quitados) =>
            quitados.Count == 0 ? string.Empty : "; metadatos quitados: " + string.Join(", ", quitados.Distinct());

        /// <summary>Crea una clave de API. Devuelve la clave completa, que no se vuelve a poder ver.</summary>
        public async Task<string> GenerarClaveAsync(string usuario, string id, string descripcion, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            var clave = ClaveApi.Generar();
            cliente.Claves.Add(new ClaveRegistro
            {
                HashClave = ClaveApi.Hash(clave),
                Sufijo = clave.Substring(clave.Length - 4),
                Descripcion = (descripcion ?? string.Empty).Trim()
            });
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "clave-crear", id, $"termina en {clave.Substring(clave.Length - 4)}");
            return clave;
        }

        public async Task RevocarClaveAsync(string usuario, string id, string claveId, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            var clave = cliente.Claves.FirstOrDefault(k => k.Id == claveId) ?? throw new ErrorAdministracion("No existe esa clave.");
            clave.Activa = false;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, "clave-revocar", id, $"termina en {clave.Sufijo}");
        }

        public async Task CambiarEstadoAsync(string usuario, string id, bool activo, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            cliente.Activo = activo;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, activo ? "activar" : "desactivar", id);
        }

        /// <summary>
        /// Autorizar producción exige escribir el texto de confirmación y que el cliente tenga certificado y software.
        /// Quitar la autorización no exige nada.
        /// </summary>
        public async Task CambiarProduccionAsync(string usuario, string id, bool permitir, string? confirmacion, CancellationToken c = default)
        {
            var cliente = await Requerir(id, c);
            if (permitir)
            {
                if (!string.Equals(confirmacion?.Trim(), TextoAutorizacionProduccion, StringComparison.Ordinal))
                {
                    throw new ErrorAdministracion($"Para autorizar producción escriba exactamente: {TextoAutorizacionProduccion}");
                }

                if (string.IsNullOrEmpty(cliente.ArchivoCertificado) || cliente.Software.Count == 0)
                {
                    throw new ErrorAdministracion("Antes de autorizar producción el cliente necesita certificado y software configurados.");
                }
            }

            cliente.PermiteProduccion = permitir;
            await _almacen.GuardarAsync(cliente, c);
            _auditoria.Registrar(usuario, permitir ? "produccion-autorizar" : "produccion-quitar", id);
        }

        private static void ValidarEmisor(EmisorConfig emisor)
        {
            if (string.IsNullOrWhiteSpace(emisor.Nit) || !emisor.Nit.All(char.IsDigit))
            {
                throw new ErrorAdministracion("El NIT solo debe llevar números, sin dígito de verificación.");
            }

            if (string.IsNullOrWhiteSpace(emisor.RazonSocial))
            {
                throw new ErrorAdministracion("La razón social es obligatoria.");
            }
        }

        private async Task<ClienteRegistro> Requerir(string id, CancellationToken c)
        {
            return await _almacen.ObtenerAsync(id, c) ?? throw new ErrorAdministracion($"No existe el cliente {id}.");
        }
    }
}
