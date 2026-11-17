using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Pos.Servicios;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.ServiciosWeb;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NominaElectronica.Enums;
using NominaElectronica.MetaDatos;

namespace Pos.Pruebas;

// Cuerpos de nómina ficticios (ni nombres ni documentos de personas reales).
public static class DatosNomina
{
    private static LocacionFisica Cali() => new()
    {
        Direccion = "CR 1 D 1 45 37",
        CodigoMunicipio = "76001",
        NombreMunicipio = "Cali",
        CodigoDepartamento = "76",
        NombreDepartamento = "Valle del Cauca",
        CodigoPais = "CO",
        NombrePais = "Colombia"
    };

    public static MetaDatosNEI Individual(string nit, string razon, string prefijo, long numero)
    {
        return new MetaDatosNEI
        {
            InformacionEnvio = new informacion_envio
            {
                TipoDocumentoElectronico = enmTipoDocElec.NominaElectronicaIndividual,
                TipoEnvio = enmTipoEnvio.Normal,
                Prefijo = prefijo,
                Numero = numero,
                FechaEnvio = new DateTime(2026, 11, 7)
            },
            Empleador = new persona
            {
                Rol = enmRolPersona.Empleador,
                TipoPersona = "JURIDICA",
                NIT = nit,
                DV = "1",
                RazonSocial = razon,
                Nombre_o_RazonSocial = razon,
                LocacionPrincipal = Cali(),
                LocacionComercial = Cali()
            },
            Empleado = new persona
            {
                Rol = enmRolPersona.Empleado,
                TipoPersona = "NATURAL",
                TipoIdDian = "13",
                Codigo = "EMP000001",
                NIT = "1000000002",
                PrimerNombre = "Trabajador",
                SegundoNombre = "De",
                PrimerApellido = "Prueba",
                SegundoApellido = "Ficticio",
                Nombre_o_RazonSocial = "Trabajador De Prueba Ficticio",
                LocacionPrincipal = Cali()
            },
            CuentaBanco = new cuenta_banco { NombreBanco = "BANCO DE PRUEBA", TipoCuenta = "Cuenta de Ahorros", NumeroCuenta = "000000000000" },
            InformacionLaboral = new informacion_laboral
            {
                SalarioBasico = 8000000M,
                TipoContrato = TipoContrato.TERMINO_INDEFINIDO.Codigo,
                ContratoID = 1,
                TipoTrabajadorCod = TipoTrabajador.DEPENDIENTE.Codigo,
                TipoTrabajadorDesc = TipoTrabajador.DEPENDIENTE.Descripcion,
                SubTipoTrabajadorCod = SubTipoTrabajador.NO_APLICA.Codigo,
                SubTipoTrabajadorDesc = SubTipoTrabajador.NO_APLICA.Descripcion
            },
            InformacionDevengados = new informacion_devevngados { DiasLaborados = 30, SueldoDevengado = 8000000M },
            PeriodoNomina = new periodo_nomina
            {
                FechaIngreso = new DateTime(2026, 10, 1),
                FechaLiquidacionInicio = new DateTime(2026, 10, 8),
                FechaLiquidacionFin = new DateTime(2026, 11, 7),
                DiasLaborados = 30
            },
            InformacionGeneral = new informacion_general { Moneda = "COP", TRM = 1, CodigoTipoPeriodo = TipoPeriodoNomina.MENSUAL.Codigo },
            InformacionPago = new informacion_pago { FormaPago = "1", MetodoPago = "42" }
        };
    }

    public static MetaDatosNEI Ajuste(string nit, string razon, string prefijo, long numero, enmTipoEnvio tipo, string cuneReferencia, string numeroReferencia)
    {
        var nom = new MetaDatosNEI(tipo, cuneReferencia, new DateTime(2026, 11, 7), numeroReferencia);
        var basico = Individual(nit, razon, prefijo, numero);
        nom.InformacionEnvio = basico.InformacionEnvio;
        nom.InformacionEnvio.TipoDocumentoElectronico = enmTipoDocElec.NominaElectronicaIndividualAjuste;
        nom.InformacionEnvio.TipoEnvio = tipo;
        nom.Empleador = basico.Empleador;
        nom.Empleado = basico.Empleado;
        nom.CuentaBanco = basico.CuentaBanco;
        nom.InformacionLaboral = basico.InformacionLaboral;
        nom.InformacionDevengados = basico.InformacionDevengados;
        nom.PeriodoNomina = basico.PeriodoNomina;
        nom.InformacionGeneral = basico.InformacionGeneral;
        nom.InformacionPago = basico.InformacionPago;
        return nom;
    }
}

public class NominaApiTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FabricaFalsa _fabrica = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-nomina-datos-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public NominaApiTests(CarpetaClientes carpeta)
    {
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:Carpeta"] = carpeta.Ruta,
                ["Datos:Carpeta"] = _datos
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(_fabrica)));
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private async Task<(HttpStatusCode Codigo, ResponseDian Respuesta)> Enviar(string clave, MetaDatosNEI nom, string accion = "EnviarNE")
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Nomina/" + accion) { Content = JsonContent.Create(nom, options: Json) };
        peticion.Headers.Add("x-api-key", clave);
        var respuesta = await _app.CreateClient().SendAsync(peticion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        return (respuesta.StatusCode, JsonSerializer.Deserialize<ResponseDian>(cuerpo, Json) ?? new ResponseDian());
    }

    private async Task<(HttpStatusCode Codigo, ResponseDian Respuesta)> Consultar(string clave, string trackId)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/Nomina/ConsultarNE/" + trackId);
        peticion.Headers.Add("x-api-key", clave);
        var respuesta = await _app.CreateClient().SendAsync(peticion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        return (respuesta.StatusCode, JsonSerializer.Deserialize<ResponseDian>(cuerpo, Json) ?? new ResponseDian());
    }

    private static MetaDatosNEI NominaA(string prefijo = "NOM", long numero = 1) => DatosNomina.Individual("900000001", "CLIENTE A SAS", prefijo, numero);

    [Fact]
    public async Task Una_nomina_en_habilitacion_va_al_set_de_pruebas_firmada_con_el_certificado_del_cliente()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, NominaA());

        Assert.Equal(HttpStatusCode.OK, codigo);
        Assert.Equal("SP00", r.statusCode);
        Assert.Equal("zip-falso", r.TrackID);
        Assert.Equal(96, r.UUID.Length); // el CUNE
        Assert.Equal(new[] { "setnom-900000001" }, _fabrica.SetsUsados.ToArray());
        Assert.Equal("CN=Cliente A", _fabrica.Llamadas.Single().Sujeto);
        Assert.Equal(AmbienteServicio.PRUEBAS, _fabrica.Llamadas.Single().Ambiente);
        Assert.Contains("Signature", System.Text.Encoding.UTF8.GetString(_fabrica.Enviados.Single()));
        Assert.True(File.Exists(Path.Combine(_datos, "A", "nomina", "xml", "NOM1.xml")));
    }

    [Fact]
    public async Task No_se_puede_reenviar_una_nomina_enviada_hasta_consultar_su_estado_y_menos_si_ya_fue_aprobada()
    {
        var (_, primera) = await Enviar(CarpetaClientes.ClaveA, NominaA());

        var (codigo2, segunda) = await Enviar(CarpetaClientes.ClaveA, NominaA());
        Assert.Equal(HttpStatusCode.BadRequest, codigo2);
        Assert.Equal("108", segunda.statusCode);
        Assert.Contains("aún no se ha consultado", segunda.statusMessage);
        Assert.Single(_fabrica.Enviados);

        var (codigoC, consulta) = await Consultar(CarpetaClientes.ClaveA, primera.TrackID);
        Assert.Equal(HttpStatusCode.OK, codigoC);
        Assert.Equal("00", consulta.statusCode);

        var (codigo3, tercera) = await Enviar(CarpetaClientes.ClaveA, NominaA());
        Assert.Equal(HttpStatusCode.BadRequest, codigo3);
        Assert.Contains("APROBADO", tercera.statusMessage);
        Assert.Single(_fabrica.Enviados);
    }

    [Fact]
    public async Task Con_el_set_ya_aceptado_la_consulta_entrega_el_estado_del_set_tal_cual()
    {
        _fabrica.SetYaAceptado = true;
        var (_, enviada) = await Enviar(CarpetaClientes.ClaveA, NominaA("NOM", 20));

        var (codigo, consulta) = await Consultar(CarpetaClientes.ClaveA, enviada.TrackID);

        // La DIAN ya no detalla cada documento: la API devuelve ese estado ("2", set aceptado) sin inventar un resultado.
        Assert.Equal(HttpStatusCode.OK, codigo);
        Assert.Equal("2", consulta.statusCode);
        Assert.Contains("Aceptado", consulta.statusMessage);
    }

    [Fact]
    public async Task CrearXML_genera_y_firma_pero_no_envia_nada()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, NominaA("NOM", 7), "CrearXML");

        Assert.Equal(HttpStatusCode.OK, codigo);
        Assert.Equal("100", r.statusCode);
        Assert.Empty(_fabrica.Enviados);
        Assert.True(File.Exists(Path.Combine(_datos, "A", "nomina", "xml", "NOM7_copia.xml")));
    }

    [Fact]
    public async Task Una_nomina_de_otro_empleador_se_rechaza_sin_enviar()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, DatosNomina.Individual("900000002", "CLIENTE B SAS", "NOM", 1));

        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal("108", r.statusCode);
        Assert.Empty(_fabrica.Enviados);
    }

    [Fact]
    public async Task Un_cliente_sin_software_de_nomina_recibe_un_mensaje_claro()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveB, DatosNomina.Individual("900000002", "CLIENTE B SAS", "NOM", 1));

        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal("108", r.statusCode);
        Assert.Contains("NÓMINA", r.statusMessage);
        Assert.Empty(_fabrica.Enviados);
    }

    [Fact]
    public async Task Consultar_un_track_desconocido_responde_404()
    {
        var (codigo, r) = await Consultar(CarpetaClientes.ClaveA, "no-existe");

        Assert.Equal(HttpStatusCode.NotFound, codigo);
        Assert.Contains(r.Errors, e => e.Codigo == "DB_NOT_FOUND");
    }

    [Fact]
    public async Task Un_cliente_no_ve_los_documentos_de_otro()
    {
        var (_, enviada) = await Enviar(CarpetaClientes.ClaveA, NominaA());

        var (codigo, _) = await Consultar(CarpetaClientes.ClaveProduccion, enviada.TrackID);

        Assert.Equal(HttpStatusCode.NotFound, codigo);
    }

    [Fact]
    public async Task Un_ajuste_exige_la_referencia_y_al_aprobarse_marca_el_original_como_reemplazado()
    {
        // Sin referencia: el tipo de documento no coincide con el tipo de envío.
        var sinReferencia = DatosNomina.Individual("900000001", "CLIENTE A SAS", "NOM", 2);
        sinReferencia.InformacionEnvio.TipoEnvio = enmTipoEnvio.Reemplazar;
        var (codigoMal, mal) = await Enviar(CarpetaClientes.ClaveA, sinReferencia);
        Assert.Equal(HttpStatusCode.BadRequest, codigoMal);
        Assert.Equal("108", mal.statusCode);

        // Original aprobado.
        var (_, original) = await Enviar(CarpetaClientes.ClaveA, NominaA("NOM", 3));
        await Consultar(CarpetaClientes.ClaveA, original.TrackID);

        // Ajuste que reemplaza al original.
        var ajuste = DatosNomina.Ajuste("900000001", "CLIENTE A SAS", "NOM", 3, enmTipoEnvio.Reemplazar, original.UUID, "NOM3");
        var (codigo, enviado) = await Enviar(CarpetaClientes.ClaveA, ajuste);
        Assert.Equal(HttpStatusCode.OK, codigo);
        Assert.Equal("SP00", enviado.statusCode);
        Assert.True(File.Exists(Path.Combine(_datos, "A", "nomina", "xml", "NOMR3.xml"))); // el número lleva la R del reemplazo

        await Consultar(CarpetaClientes.ClaveA, enviado.TrackID);
        var original5 = File.ReadAllText(Path.Combine(_datos, "A", "nomina", "docs", "102_NOM3.json"));
        Assert.Contains("\"Estado\": 5", original5); // REEMPLAZADO
    }

    [Fact]
    public async Task Un_ajuste_que_elimina_marca_el_original_como_eliminado()
    {
        var (_, original) = await Enviar(CarpetaClientes.ClaveA, NominaA("NOM", 4));
        await Consultar(CarpetaClientes.ClaveA, original.TrackID);

        var ajuste = DatosNomina.Ajuste("900000001", "CLIENTE A SAS", "NOM", 4, enmTipoEnvio.Eliminar, original.UUID, "NOM4");
        var (codigo, enviado) = await Enviar(CarpetaClientes.ClaveA, ajuste);
        Assert.Equal(HttpStatusCode.OK, codigo);

        await Consultar(CarpetaClientes.ClaveA, enviado.TrackID);
        Assert.Contains("\"Estado\": 4", File.ReadAllText(Path.Combine(_datos, "A", "nomina", "docs", "102_NOM4.json"))); // ELIMINADO
        Assert.True(File.Exists(Path.Combine(_datos, "A", "nomina", "xml", "NOME4.xml")));
    }

    [Fact]
    public async Task Con_produccion_autorizada_el_envio_es_sincrono_y_va_a_produccion()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveProduccion, DatosNomina.Individual("900000003", "CLIENTE P SAS", "NOM", 1));

        Assert.Equal(HttpStatusCode.OK, codigo);
        Assert.Equal("00", r.statusCode);
        Assert.Equal("uuid-falso", r.TrackID);
        Assert.Equal(AmbienteServicio.PRODUCCION, _fabrica.Llamadas.Single().Ambiente);
        Assert.Empty(_fabrica.SetsUsados);
    }

    [Fact]
    public async Task Sin_clave_de_api_no_se_envia_nada()
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/api/v1/Nomina/EnviarNE", NominaA(), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(_fabrica.Enviados);
    }
}
