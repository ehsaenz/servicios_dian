using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DIAN.Tenants
{
    /// <summary>
    /// Almacén de clientes en MySQL (ver EsquemaMySql.sql). Pensado para varias instancias de la API y de la app Admin
    /// sobre la misma base: cada una guarda en memoria la lista de clientes y la vuelve a leer cuando otra la cambió.
    /// Los archivos (certificado, logo) viven en la base y se copian a una carpeta local de caché, porque el resto del
    /// sistema los lee por ruta. Un cambio hecho por otra instancia se nota en <c>comprobarCada</c> segundos como máximo.
    /// </summary>
    public sealed class MySqlTenantStore : ITenantStore
    {
        private static readonly JsonSerializerOptions Copia = new JsonSerializerOptions();

        private readonly string _cadena;
        private readonly string _cache;
        private readonly TimeSpan _comprobarCada;
        private readonly SemaphoreSlim _recarga = new SemaphoreSlim(1, 1);
        private IReadOnlyList<ClienteRegistro> _lista = new List<ClienteRegistro>();
        private string? _firma;
        private DateTime _ultimaComprobacion = DateTime.MinValue;

        public MySqlTenantStore(string cadenaConexion, string carpetaCache, TimeSpan? comprobarCada = null)
        {
            _cadena = cadenaConexion;
            _cache = carpetaCache;
            _comprobarCada = comprobarCada ?? TimeSpan.FromSeconds(2);
            CrearCarpeta(_cache);
        }

        // ---------- esquema ----------

        /// <summary>Crea las tablas si no existen. Se puede repetir sin riesgo.</summary>
        public static async Task CrearEsquemaAsync(string cadenaConexion)
        {
            string sql;
            using (var flujo = Assembly.GetExecutingAssembly().GetManifestResourceStream("DIAN.Tenants.EsquemaMySql.sql")
                              ?? throw new InvalidOperationException("Falta el esquema embebido."))
            using (var lector = new StreamReader(flujo))
            {
                sql = await lector.ReadToEndAsync();
            }

            using var conexion = new MySqlConnection(cadenaConexion);
            await conexion.OpenAsync();
            foreach (var sentencia in sql.Split(';').Select(Limpiar).Where(s => s.Length > 0))
            {
                using var comando = new MySqlCommand(sentencia, conexion);
                await comando.ExecuteNonQueryAsync();
            }
        }

        private static string Limpiar(string sentencia) =>
            string.Join("\n", sentencia.Split('\n').Where(l => !l.TrimStart().StartsWith("--"))).Trim();

        // ---------- lectura ----------

        public async Task<IReadOnlyList<ClienteRegistro>> ListarAsync(CancellationToken cancelacion = default)
        {
            if (_firma != null && DateTime.UtcNow - _ultimaComprobacion < _comprobarCada)
            {
                return _lista;
            }

            await _recarga.WaitAsync(cancelacion);
            try
            {
                using var conexion = await AbrirAsync(cancelacion);
                var firma = await FirmaAsync(conexion, cancelacion);
                _ultimaComprobacion = DateTime.UtcNow;
                if (firma == _firma)
                {
                    return _lista;
                }

                _lista = await LeerTodoAsync(conexion, cancelacion);
                _firma = firma;
                return _lista;
            }
            finally
            {
                _recarga.Release();
            }
        }

        /// <summary>Devuelve una copia: quien la edite no altera la lista en memoria si el guardado falla.</summary>
        public async Task<ClienteRegistro?> ObtenerAsync(string id, CancellationToken cancelacion = default)
        {
            var cliente = (await ListarAsync(cancelacion)).FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            return cliente == null ? null : JsonSerializer.Deserialize<ClienteRegistro>(JsonSerializer.Serialize(cliente, Copia), Copia);
        }

        private static async Task<string> FirmaAsync(MySqlConnection conexion, CancellationToken c)
        {
            // Cualquier cambio sube alguna Version, así que esta firma cambia con cada guardado de cualquier instancia.
            const string sql =
                "SELECT (SELECT COUNT(*) FROM dian_clientes), (SELECT COALESCE(SUM(Version), 0) FROM dian_clientes), " +
                "(SELECT COUNT(*) FROM dian_archivos), (SELECT COALESCE(SUM(Version), 0) FROM dian_archivos)";
            using var comando = new MySqlCommand(sql, conexion);
            using var lector = await comando.ExecuteReaderAsync(c);
            await lector.ReadAsync(c);
            return string.Join("|", Enumerable.Range(0, 4).Select(i => Convert.ToInt64(lector[i])));
        }

        private async Task<IReadOnlyList<ClienteRegistro>> LeerTodoAsync(MySqlConnection conexion, CancellationToken c)
        {
            var clientes = new List<ClienteRegistro>();
            using (var comando = new MySqlCommand(
                "SELECT IdCliente, Version, Activo, PermiteProduccion, Nit, DV, RazonSocial, MatriculaMercantil, Email, NumeroTel, PerfilCorreo, " +
                "ArchivoCertificado, ClaveCertificadoProtegida, CertificadoSujeto, CertificadoVence, ArchivoLogo FROM dian_clientes ORDER BY IdCliente", conexion))
            using (var lector = await comando.ExecuteReaderAsync(c))
            {
                while (await lector.ReadAsync(c))
                {
                    clientes.Add(new ClienteRegistro
                    {
                        Id = lector.GetString(0),
                        Version = Convert.ToInt32(lector[1]),
                        Activo = Convert.ToInt32(lector[2]) != 0,
                        PermiteProduccion = Convert.ToInt32(lector[3]) != 0,
                        Emisor = new EmisorConfig
                        {
                            Nit = lector.GetString(4),
                            DV = lector.GetString(5),
                            RazonSocial = lector.GetString(6),
                            MatriculaMercantil = lector.GetString(7),
                            Email = lector.GetString(8),
                            NumeroTel = lector.GetString(9)
                        },
                        PerfilCorreo = Texto(lector, 10),
                        ArchivoCertificado = Texto(lector, 11),
                        ClaveCertificadoProtegida = Texto(lector, 12),
                        CertificadoSujeto = Texto(lector, 13),
                        CertificadoVenceUtc = lector.IsDBNull(14) ? null : DateTime.SpecifyKind(lector.GetDateTime(14), DateTimeKind.Utc),
                        ArchivoLogo = Texto(lector, 15)
                    });
                }
            }

            var porId = clientes.ToDictionary(x => x.Id, StringComparer.Ordinal);

            using (var comando = new MySqlCommand("SELECT ClienteId, IdClave, HashClave, Sufijo, Descripcion, Activa, Creada FROM dian_claves ORDER BY Creada, IdClave", conexion))
            using (var lector = await comando.ExecuteReaderAsync(c))
            {
                while (await lector.ReadAsync(c))
                {
                    if (porId.TryGetValue(lector.GetString(0), out var cliente))
                    {
                        cliente.Claves.Add(new ClaveRegistro
                        {
                            Id = lector.GetString(1),
                            HashClave = lector.GetString(2),
                            Sufijo = lector.GetString(3),
                            Descripcion = lector.GetString(4),
                            Activa = Convert.ToInt32(lector[5]) != 0,
                            CreadaUtc = DateTime.SpecifyKind(lector.GetDateTime(6), DateTimeKind.Utc)
                        });
                    }
                }
            }

            using (var comando = new MySqlCommand("SELECT ClienteId, TipoSoftware, Identificador, PinProtegido, AmbientePruebas, TestSetId FROM dian_software ORDER BY ClienteId, TipoSoftware", conexion))
            using (var lector = await comando.ExecuteReaderAsync(c))
            {
                while (await lector.ReadAsync(c))
                {
                    if (porId.TryGetValue(lector.GetString(0), out var cliente))
                    {
                        cliente.Software.Add(new SoftwareRegistro
                        {
                            TipoSoftware = (TipoSoftware)Convert.ToInt32(lector[1]),
                            Identificador = lector.GetString(2),
                            PinProtegido = lector.GetString(3),
                            AmbientePruebas = Convert.ToInt32(lector[4]) != 0,
                            TestSetId = lector.GetString(5)
                        });
                    }
                }
            }

            using (var comando = new MySqlCommand("SELECT ClienteId, Matricula, Nombre, RegimenFiscal, ArchivoLogo FROM dian_sucursales ORDER BY ClienteId, Matricula", conexion))
            using (var lector = await comando.ExecuteReaderAsync(c))
            {
                while (await lector.ReadAsync(c))
                {
                    if (porId.TryGetValue(lector.GetString(0), out var cliente))
                    {
                        cliente.Sucursales.Add(new SucursalRegistro
                        {
                            MatriculaMercantil = lector.GetString(1),
                            Nombre = lector.GetString(2),
                            RegimenFiscal = lector.GetString(3),
                            ArchivoLogo = Texto(lector, 4)
                        });
                    }
                }
            }

            using (var comando = new MySqlCommand("SELECT ClienteId, Url, SecretoProtegido FROM dian_webhooks", conexion))
            using (var lector = await comando.ExecuteReaderAsync(c))
            {
                while (await lector.ReadAsync(c))
                {
                    if (porId.TryGetValue(lector.GetString(0), out var cliente))
                    {
                        cliente.WebhookUrl = lector.GetString(1);
                        cliente.WebhookSecretoProtegido = lector.GetString(2);
                    }
                }
            }

            await SincronizarArchivosAsync(conexion, c);
            return clientes;
        }

        private static string? Texto(DbDataReader lector, int posicion) => lector.IsDBNull(posicion) ? null : lector.GetString(posicion);

        // ---------- escritura ----------

        public async Task GuardarAsync(ClienteRegistro cliente, CancellationToken cancelacion = default)
        {
            if (!ClienteRegistro.EsIdValido(cliente.Id))
            {
                throw new ArgumentException("El id del cliente solo admite letras, números, guion y guion bajo (1 a 40 caracteres).");
            }

            using var conexion = await AbrirAsync(cancelacion);
            using var transaccion = await conexion.BeginTransactionAsync(cancelacion);

            if (cliente.Version == 0)
            {
                try
                {
                    using var insertar = Comando(conexion, transaccion,
                        "INSERT INTO dian_clientes (IdCliente, Version, Activo, PermiteProduccion, Nit, DV, RazonSocial, MatriculaMercantil, Email, NumeroTel, " +
                        "PerfilCorreo, ArchivoCertificado, ClaveCertificadoProtegida, CertificadoSujeto, CertificadoVence, ArchivoLogo) " +
                        "VALUES (@id, 1, @activo, @prod, @nit, @dv, @razon, @matricula, @email, @tel, @perfil, @archcert, @clavecert, @sujeto, @vence, @logo)");
                    Parametros(insertar, cliente);
                    await insertar.ExecuteNonQueryAsync(cancelacion);
                }
                catch (MySqlException ex) when (ex.Number == 1062)
                {
                    throw new ErrorAdministracion($"Ya existe un cliente con el id {cliente.Id}.");
                }
            }
            else
            {
                using var actualizar = Comando(conexion, transaccion,
                    "UPDATE dian_clientes SET Version = Version + 1, Activo = @activo, PermiteProduccion = @prod, Nit = @nit, DV = @dv, RazonSocial = @razon, " +
                    "MatriculaMercantil = @matricula, Email = @email, NumeroTel = @tel, PerfilCorreo = @perfil, ArchivoCertificado = @archcert, " +
                    "ClaveCertificadoProtegida = @clavecert, CertificadoSujeto = @sujeto, CertificadoVence = @vence, ArchivoLogo = @logo " +
                    "WHERE IdCliente = @id AND Version = @version");
                Parametros(actualizar, cliente);
                actualizar.Parameters.AddWithValue("@version", cliente.Version);
                if (await actualizar.ExecuteNonQueryAsync(cancelacion) == 0)
                {
                    throw new ErrorAdministracion("El cliente cambió mientras lo editaba. Recargue la página y repita el cambio.");
                }
            }

            using (var borrar = Comando(conexion, transaccion, "DELETE FROM dian_claves WHERE ClienteId = @id"))
            {
                borrar.Parameters.AddWithValue("@id", cliente.Id);
                await borrar.ExecuteNonQueryAsync(cancelacion);
            }

            foreach (var clave in cliente.Claves)
            {
                using var insertar = Comando(conexion, transaccion,
                    "INSERT INTO dian_claves (ClienteId, IdClave, HashClave, Sufijo, Descripcion, Activa, Creada) VALUES (@id, @idclave, @hash, @sufijo, @descripcion, @activa, @creada)");
                insertar.Parameters.AddWithValue("@id", cliente.Id);
                insertar.Parameters.AddWithValue("@idclave", clave.Id);
                insertar.Parameters.AddWithValue("@hash", clave.HashClave);
                insertar.Parameters.AddWithValue("@sufijo", clave.Sufijo);
                insertar.Parameters.AddWithValue("@descripcion", clave.Descripcion);
                insertar.Parameters.AddWithValue("@activa", clave.Activa ? 1 : 0);
                insertar.Parameters.AddWithValue("@creada", clave.CreadaUtc);
                await insertar.ExecuteNonQueryAsync(cancelacion);
            }

            using (var borrar = Comando(conexion, transaccion, "DELETE FROM dian_software WHERE ClienteId = @id"))
            {
                borrar.Parameters.AddWithValue("@id", cliente.Id);
                await borrar.ExecuteNonQueryAsync(cancelacion);
            }

            foreach (var software in cliente.Software)
            {
                using var insertar = Comando(conexion, transaccion,
                    "INSERT INTO dian_software (ClienteId, TipoSoftware, Identificador, PinProtegido, AmbientePruebas, TestSetId) VALUES (@id, @tipo, @identificador, @pin, @pruebas, @testset)");
                insertar.Parameters.AddWithValue("@id", cliente.Id);
                insertar.Parameters.AddWithValue("@tipo", (int)software.TipoSoftware);
                insertar.Parameters.AddWithValue("@identificador", software.Identificador);
                insertar.Parameters.AddWithValue("@pin", software.PinProtegido);
                insertar.Parameters.AddWithValue("@pruebas", software.AmbientePruebas ? 1 : 0);
                insertar.Parameters.AddWithValue("@testset", software.TestSetId);
                await insertar.ExecuteNonQueryAsync(cancelacion);
            }

            using (var borrar = Comando(conexion, transaccion, "DELETE FROM dian_sucursales WHERE ClienteId = @id"))
            {
                borrar.Parameters.AddWithValue("@id", cliente.Id);
                await borrar.ExecuteNonQueryAsync(cancelacion);
            }

            foreach (var sucursal in cliente.Sucursales)
            {
                using var insertar = Comando(conexion, transaccion,
                    "INSERT INTO dian_sucursales (ClienteId, Matricula, Nombre, RegimenFiscal, ArchivoLogo) VALUES (@id, @matricula, @nombre, @regimen, @logo)");
                insertar.Parameters.AddWithValue("@id", cliente.Id);
                insertar.Parameters.AddWithValue("@matricula", sucursal.MatriculaMercantil);
                insertar.Parameters.AddWithValue("@nombre", sucursal.Nombre);
                insertar.Parameters.AddWithValue("@regimen", sucursal.RegimenFiscal);
                insertar.Parameters.AddWithValue("@logo", (object?)sucursal.ArchivoLogo ?? DBNull.Value);
                await insertar.ExecuteNonQueryAsync(cancelacion);
            }

            using (var borrar = Comando(conexion, transaccion, "DELETE FROM dian_webhooks WHERE ClienteId = @id"))
            {
                borrar.Parameters.AddWithValue("@id", cliente.Id);
                await borrar.ExecuteNonQueryAsync(cancelacion);
            }

            if (!string.IsNullOrEmpty(cliente.WebhookUrl))
            {
                using var insertar = Comando(conexion, transaccion, "INSERT INTO dian_webhooks (ClienteId, Url, SecretoProtegido) VALUES (@id, @url, @secreto)");
                insertar.Parameters.AddWithValue("@id", cliente.Id);
                insertar.Parameters.AddWithValue("@url", cliente.WebhookUrl);
                insertar.Parameters.AddWithValue("@secreto", cliente.WebhookSecretoProtegido ?? string.Empty);
                await insertar.ExecuteNonQueryAsync(cancelacion);
            }

            await transaccion.CommitAsync(cancelacion);
            cliente.Version = cliente.Version + 1;
            Invalidar();
        }

        public async Task<string> GuardarArchivoAsync(string idCliente, string nombre, byte[] contenido, CancellationToken cancelacion = default)
        {
            var ruta = RutaArchivo(idCliente, nombre);
            nombre = Path.GetFileName(nombre);

            using var conexion = await AbrirAsync(cancelacion);
            using var transaccion = await conexion.BeginTransactionAsync(cancelacion);

            int version;
            using (var leer = Comando(conexion, transaccion, "SELECT Version FROM dian_archivos WHERE ClienteId = @id AND Nombre = @nombre FOR UPDATE"))
            {
                leer.Parameters.AddWithValue("@id", idCliente);
                leer.Parameters.AddWithValue("@nombre", nombre);
                var actual = await leer.ExecuteScalarAsync(cancelacion);
                version = actual == null ? 1 : Convert.ToInt32(actual) + 1;

                using var escribir = Comando(conexion, transaccion, actual == null
                    ? "INSERT INTO dian_archivos (ClienteId, Nombre, Version, Contenido) VALUES (@id, @nombre, @version, @contenido)"
                    : "UPDATE dian_archivos SET Version = @version, Contenido = @contenido WHERE ClienteId = @id AND Nombre = @nombre");
                escribir.Parameters.AddWithValue("@id", idCliente);
                escribir.Parameters.AddWithValue("@nombre", nombre);
                escribir.Parameters.AddWithValue("@version", version);
                escribir.Parameters.AddWithValue("@contenido", contenido);
                await escribir.ExecuteNonQueryAsync(cancelacion);
            }

            await transaccion.CommitAsync(cancelacion);
            await EscribirCacheAsync(idCliente, nombre, version, contenido, cancelacion);
            Invalidar();
            return ruta;
        }

        public async Task EliminarArchivoAsync(string idCliente, string nombre, CancellationToken cancelacion = default)
        {
            var ruta = RutaArchivo(idCliente, nombre);
            using var conexion = await AbrirAsync(cancelacion);
            using var comando = Comando(conexion, null, "DELETE FROM dian_archivos WHERE ClienteId = @id AND Nombre = @nombre");
            comando.Parameters.AddWithValue("@id", idCliente);
            comando.Parameters.AddWithValue("@nombre", Path.GetFileName(nombre));
            await comando.ExecuteNonQueryAsync(cancelacion);
            File.Delete(ruta);
            File.Delete(ruta + ".ver");
            Invalidar();
        }

        public string RutaArchivo(string idCliente, string nombre)
        {
            if (!ClienteRegistro.EsIdValido(idCliente))
            {
                throw new ArgumentException("El id del cliente solo admite letras, números, guion y guion bajo (1 a 40 caracteres).");
            }

            return Path.Combine(_cache, idCliente, Path.GetFileName(nombre));
        }

        // ---------- caché local de archivos ----------

        /// <summary>Deja en la carpeta local los archivos que cambiaron en la base (comparando la versión de cada uno).</summary>
        private async Task SincronizarArchivosAsync(MySqlConnection conexion, CancellationToken c)
        {
            var pendientes = new List<(string Cliente, string Nombre, int Version)>();
            using (var comando = new MySqlCommand("SELECT ClienteId, Nombre, Version FROM dian_archivos", conexion))
            using (var lector = await comando.ExecuteReaderAsync(c))
            {
                while (await lector.ReadAsync(c))
                {
                    var cliente = lector.GetString(0);
                    var nombre = lector.GetString(1);
                    var version = Convert.ToInt32(lector[2]);
                    var ruta = RutaArchivo(cliente, nombre);
                    if (!File.Exists(ruta) || !File.Exists(ruta + ".ver") || File.ReadAllText(ruta + ".ver").Trim() != version.ToString())
                    {
                        pendientes.Add((cliente, nombre, version));
                    }
                }
            }

            foreach (var (cliente, nombre, version) in pendientes)
            {
                using var comando = new MySqlCommand("SELECT Contenido FROM dian_archivos WHERE ClienteId = @id AND Nombre = @nombre", conexion);
                comando.Parameters.AddWithValue("@id", cliente);
                comando.Parameters.AddWithValue("@nombre", nombre);
                if (await comando.ExecuteScalarAsync(c) is byte[] contenido)
                {
                    await EscribirCacheAsync(cliente, nombre, version, contenido, c);
                }
            }
        }

        private async Task EscribirCacheAsync(string idCliente, string nombre, int version, byte[] contenido, CancellationToken c)
        {
            var ruta = RutaArchivo(idCliente, nombre);
            CrearCarpeta(Path.GetDirectoryName(ruta)!);
            var temporal = ruta + ".tmp";
            await File.WriteAllBytesAsync(temporal, contenido, c);
            File.Move(temporal, ruta, true);
            await File.WriteAllTextAsync(ruta + ".ver", version.ToString(), c);
            Restringir(ruta);
            Restringir(ruta + ".ver");
        }

        private static void CrearCarpeta(string ruta)
        {
            Directory.CreateDirectory(ruta);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(ruta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        private static void Restringir(string ruta)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(ruta, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        // ---------- utilidades ----------

        private void Invalidar()
        {
            _firma = null;
        }

        private async Task<MySqlConnection> AbrirAsync(CancellationToken c)
        {
            var conexion = new MySqlConnection(_cadena);
            await conexion.OpenAsync(c);
            return conexion;
        }

        private static MySqlCommand Comando(MySqlConnection conexion, DbTransaction? transaccion, string sql)
        {
            return new MySqlCommand(sql, conexion, (MySqlTransaction?)transaccion);
        }

        private static void Parametros(MySqlCommand comando, ClienteRegistro c)
        {
            comando.Parameters.AddWithValue("@id", c.Id);
            comando.Parameters.AddWithValue("@activo", c.Activo ? 1 : 0);
            comando.Parameters.AddWithValue("@prod", c.PermiteProduccion ? 1 : 0);
            comando.Parameters.AddWithValue("@nit", c.Emisor.Nit);
            comando.Parameters.AddWithValue("@dv", c.Emisor.DV);
            comando.Parameters.AddWithValue("@razon", c.Emisor.RazonSocial);
            comando.Parameters.AddWithValue("@matricula", c.Emisor.MatriculaMercantil);
            comando.Parameters.AddWithValue("@email", c.Emisor.Email);
            comando.Parameters.AddWithValue("@tel", c.Emisor.NumeroTel);
            comando.Parameters.AddWithValue("@perfil", (object?)c.PerfilCorreo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@archcert", (object?)c.ArchivoCertificado ?? DBNull.Value);
            comando.Parameters.AddWithValue("@clavecert", (object?)c.ClaveCertificadoProtegida ?? DBNull.Value);
            comando.Parameters.AddWithValue("@sujeto", (object?)c.CertificadoSujeto ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vence", (object?)c.CertificadoVenceUtc ?? DBNull.Value);
            comando.Parameters.AddWithValue("@logo", (object?)c.ArchivoLogo ?? DBNull.Value);
        }
    }
}
