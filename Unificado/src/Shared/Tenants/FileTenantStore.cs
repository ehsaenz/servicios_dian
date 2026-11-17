using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Tenants
{
    /// <summary>
    /// Guarda cada cliente en <c>{raiz}\clientes\{id}\cliente.json</c> junto con su certificado y su logo.
    /// Es el almacén de arranque (un servidor, pocos clientes); el de MySQL lo reemplazará detrás de <see cref="ITenantStore"/>.
    /// </summary>
    public sealed class FileTenantStore : ITenantStore
    {
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _clientes;
        private readonly SemaphoreSlim _escritura = new SemaphoreSlim(1, 1);
        private (string Firma, IReadOnlyList<ClienteRegistro> Lista)? _cache;

        public FileTenantStore(string raiz)
        {
            _clientes = Path.Combine(raiz, "clientes");
            Directory.CreateDirectory(_clientes);
        }

        public string RutaArchivo(string idCliente, string nombre)
        {
            Validar(idCliente);
            return Path.Combine(_clientes, idCliente, Path.GetFileName(nombre));
        }

        public Task<IReadOnlyList<ClienteRegistro>> ListarAsync(CancellationToken cancelacion = default)
        {
            var archivos = Directory.GetDirectories(_clientes)
                .Select(d => Path.Combine(d, "cliente.json"))
                .Where(File.Exists)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // La lista se guarda en memoria y se vuelve a leer solo si algún archivo cambió.
            var firma = string.Join("|", archivos.Select(a => a + ":" + File.GetLastWriteTimeUtc(a).Ticks));
            if (_cache is { } actual && actual.Firma == firma)
            {
                return Task.FromResult(actual.Lista);
            }

            var lista = archivos
                .Select(a => JsonSerializer.Deserialize<ClienteRegistro>(File.ReadAllText(a), Opciones)!)
                .ToList();
            IReadOnlyList<ClienteRegistro> resultado = lista;
            _cache = (firma, resultado);
            return Task.FromResult(resultado);
        }

        public async Task<ClienteRegistro?> ObtenerAsync(string id, CancellationToken cancelacion = default)
        {
            var todos = await ListarAsync(cancelacion);
            return todos.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public async Task GuardarAsync(ClienteRegistro cliente, CancellationToken cancelacion = default)
        {
            Validar(cliente.Id);
            await _escritura.WaitAsync(cancelacion);
            try
            {
                var carpeta = Path.Combine(_clientes, cliente.Id);
                Directory.CreateDirectory(carpeta);
                var destino = Path.Combine(carpeta, "cliente.json");
                var temporal = destino + ".tmp";
                await File.WriteAllTextAsync(temporal, JsonSerializer.Serialize(cliente, Opciones), cancelacion);
                File.Move(temporal, destino, true);
                _cache = null;
            }
            finally
            {
                _escritura.Release();
            }
        }

        public async Task<string> GuardarArchivoAsync(string idCliente, string nombre, byte[] contenido, CancellationToken cancelacion = default)
        {
            var ruta = RutaArchivo(idCliente, nombre);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            var temporal = ruta + ".tmp";
            await File.WriteAllBytesAsync(temporal, contenido, cancelacion);
            File.Move(temporal, ruta, true);
            return ruta;
        }

        public Task EliminarArchivoAsync(string idCliente, string nombre, CancellationToken cancelacion = default)
        {
            File.Delete(RutaArchivo(idCliente, nombre));
            return Task.CompletedTask;
        }

        private static void Validar(string id)
        {
            if (!ClienteRegistro.EsIdValido(id))
            {
                throw new ArgumentException("El id del cliente solo admite letras, números, guion y guion bajo (1 a 40 caracteres).");
            }
        }
    }
}
