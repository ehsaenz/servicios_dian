using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace eFacturacionColombia_NETCore.Documentos
{
    public class GeneradorRips
    {
        public TransaccionRips Transaccion { get; private set; }

        public GeneradorRips(TransaccionRips transaccion)
        {
            Transaccion = new TransaccionRips
            {
                Usuarios = transaccion.Usuarios,
                NumeroDocumentoIdObligado = transaccion.NumeroDocumentoIdObligado,
                NumeroFactura = transaccion.NumeroFactura,
                TipoNota = transaccion.TipoNota,
                NumeroNota = transaccion.NumeroNota
            };
        }

        public GeneradorRips()
        {
            Transaccion = new TransaccionRips();
        }

        public GeneradorRips ConTransaccion(TransaccionRips transaccion)
        {
            Transaccion.NumeroDocumentoIdObligado = transaccion.NumeroDocumentoIdObligado;
            Transaccion.NumeroFactura = transaccion.NumeroFactura;
            Transaccion.TipoNota = transaccion.TipoNota;
            Transaccion.NumeroNota = transaccion.NumeroNota;

            return this;
        }

        public GeneradorRips ConUsuario(UsuarioRips usuario)
        {
            Transaccion.Usuarios = new Collection<UsuarioRips>
            {
                usuario
            };

            return this;
        }

        public GeneradorRips AgregarTransaccion(TransaccionRips transaccion)
        {
            return ConTransaccion(transaccion);
        }

        public GeneradorRips AgregarUsuario(UsuarioRips usuario)
        {
            Transaccion.Usuarios.Add(usuario);

            return this;
        }

        public GeneradorRips AgregarConsulta(ConsultaRips consulta)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.Consultas.Add(consulta);

            return this;
        }

        public GeneradorRips AgregarProcedimiento(ProcedimientoRips procedimiento)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.Procedimientos.Add(procedimiento);

            return this;
        }

        public GeneradorRips AgregarUrgencia(UrgenciaRips urgencia)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.Urgencias.Add(urgencia);

            return this;
        }

        public GeneradorRips AgregarHospitalizacion(HospitalizacionRips hospitalizacion)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.Hospitalizaciones.Add(hospitalizacion);

            return this;
        }

        public GeneradorRips AgregarRecienNacido(RecienNacidoRips recienNacido)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.RecienNacidos.Add(recienNacido);

            return this;
        }

        public GeneradorRips AgregarMedicamento(MedicamentoRips medicamento)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.Medicamentos.Add(medicamento);

            return this;
        }

        public GeneradorRips AgregarOtroServicio(OtroServicioRips otroServicio)
        {
            var indexUsuario = Transaccion.Usuarios.Count - 1;

            Transaccion.Usuarios[indexUsuario].Servicios.OtrosServicios.Add(otroServicio);

            return this;
        }

        public TransaccionRips Obtener()
        {
            return Transaccion;
        }

        public string Serializar()
        {
            return Transaccion.Serializar();
        }
    }

    public class TransaccionRips
    {
        [JsonProperty("numDocumentoldObligado")]
        public string NumeroDocumentoIdObligado { get; set; }

        [JsonProperty("numFactura")]
        public string NumeroFactura { get; set; }

        [JsonProperty("tipoNota")]
        public string TipoNota { get; set; }

        [JsonProperty("numNota")]
        public string NumeroNota { get; set; }

        [JsonProperty("usuarios")]
        public Collection<UsuarioRips> Usuarios { get; set; } = new Collection<UsuarioRips>();

        public string Serializar()
        {
            var formatting = Formatting.Indented;

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            };

            return JsonConvert.SerializeObject(this, formatting, settings);
        }


        public static TransaccionRips Deserializar(string json)
        {
            return JsonConvert.DeserializeObject<TransaccionRips>(json);
        }
    }

    public class UsuarioRips
    {
        [JsonProperty("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; }

        [JsonProperty("numDocumentoIdentificacion")]
        public string NumeroDocumentoIdentificacion { get; set; }

        [JsonProperty("tipoUsuario")]
        public string TipoUsuario { get; set; }

        [JsonProperty("fechaNacimiento")]
        public string FechaNacimiento { get; set; }

        [JsonProperty("codSexo")]
        public string CodigoSexo { get; set; }

        [JsonProperty("codPaisResidencia")]
        public string CodigoPaisResidencia { get; set; }

        [JsonProperty("codMunicipioResidencia")]
        public string CodigoMunicipioResidencia { get; set; }

        [JsonProperty("codZonaTerritorialResidencia")]
        public string CodigoZonaTerritorialResidencia { get; set; }

        [JsonProperty("incapacidad")]
        public string Incapacidad { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }

        [JsonProperty("codPaisOrigen")]
        public string CodigoPaisOrigen { get; set; }

        [JsonProperty("servicios")]
        public ServiciosRips Servicios { get; set; } = new ServiciosRips();
    }

    public class ServiciosRips
    {
        [JsonProperty("consultas")]
        public Collection<ConsultaRips> Consultas { get; set; } = new Collection<ConsultaRips>();

        [JsonProperty("procedimientos")]
        public Collection<ProcedimientoRips> Procedimientos { get; set; } = new Collection<ProcedimientoRips>();

        [JsonProperty("urgencias")]
        public Collection<UrgenciaRips> Urgencias { get; set; } = new Collection<UrgenciaRips>();

        [JsonProperty("hospitalizacion")]
        public Collection<HospitalizacionRips> Hospitalizaciones { get; set; } = new Collection<HospitalizacionRips>();

        [JsonProperty("recienNacidos")]
        public Collection<RecienNacidoRips> RecienNacidos { get; set; } = new Collection<RecienNacidoRips>();

        [JsonProperty("medicamentos")]
        public Collection<MedicamentoRips> Medicamentos { get; set; } = new Collection<MedicamentoRips>();

        [JsonProperty("otroServicios")]
        public Collection<OtroServicioRips> OtrosServicios { get; set; } = new Collection<OtroServicioRips>();
    }

    public class ConsultaRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("fechaInicioAtencion")]
        public string FechaInicioAtencion { get; set; }

        [JsonProperty("numAutorizacion")]
        public string NumeroAutorizacion { get; set; }

        [JsonProperty("codConsulta")]
        public string CodigoConsulta { get; set; }

        [JsonProperty("modalidadGrupoServicioTecSal")]
        public string ModalidadAtencion { get; set; }

        [JsonProperty("grupoServicios")]
        public string GrupoServicios { get; set; }

        [JsonProperty("codServicio")]
        public long CodigoServicio { get; set; }

        [JsonProperty("finalidadTecnologiaSalud")]
        public string FinalidadTecnologiaSalud { get; set; }

        [JsonProperty("causaMotivoAtencion")]
        public string CausaMotivoAtencion { get; set; }

        [JsonProperty("codDiagnosticoPrincipal")]
        public string CodigoDiagnosticoPrincipal { get; set; }

        [JsonProperty("codDiagnosticoRelacionado1")]
        public string CodigoDiagnosticoRelacionado1 { get; set; }

        [JsonProperty("codDiagnosticoRelacionado2")]
        public string CodigoDiagnosticoRelacionado2 { get; set; }

        [JsonProperty("codDiagnosticoRelacionado3")]
        public string CodigoDiagnosticoRelacionado3 { get; set; }

        [JsonProperty("tipoDiagnosticoPrincipal")]
        public string TipoDiagnosticoPrincipal { get; set; }

        [JsonProperty("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; }

        [JsonProperty("numDocumentoIdentificacion")]
        public string NumeroDocumentoIdentificacion { get; set; }

        [JsonProperty("vrServicio")]
        public decimal ValorServicio { get; set; }

        [JsonProperty("conceptoRecaudo")]
        public string ConceptoRecaudo { get; set; }

        [JsonProperty("valorPagoModerador")]
        public decimal ValorPagoModerador { get; set; }

        [JsonProperty("numFEVPagoModerador")]
        public string NumeroFacturaPagoModerador { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }

    public class ProcedimientoRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("fechaInicioAtencion")]
        public string FechaInicioAtencion { get; set; }

        [JsonProperty("idMIPRES")]
        public string IdMipres { get; set; }

        [JsonProperty("numAutorizacion")]
        public string NumeroAutorizacion { get; set; }

        [JsonProperty("codProcedimiento")]
        public string CodigoProcedimiento { get; set; }

        [JsonProperty("viaIngresoServicioSalud")]
        public string ViaIngresoServicioSalud { get; set; }

        [JsonProperty("modalidadGrupoServicioTecSal")]
        public string ModalidadAtencion { get; set; }

        [JsonProperty("grupoServicios")]
        public string GrupoServicios { get; set; }

        [JsonProperty("codServicio")]
        public long CodigoServicio { get; set; }

        [JsonProperty("finalidadTecnologiaSalud")]
        public string FinalidadTecnologiaSalud { get; set; }

        [JsonProperty("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; }

        [JsonProperty("numDocumentoIdentificacion")]
        public string NumeroDocumentoIdentificacion { get; set; }

        [JsonProperty("codDiagnosticoPrincipal")]
        public string CodigoDiagnosticoPrincipal { get; set; }

        [JsonProperty("codDiagnosticoRelacionado")]
        public string CodigoDiagnosticoRelacionado { get; set; }

        [JsonProperty("codComplicacion")]
        public string CodigoComplicacion { get; set; }

        [JsonProperty("vrServicio")]
        public decimal ValorServicio { get; set; }

        [JsonProperty("conceptoRecaudo")]
        public string ConceptoRecaudo { get; set; }

        [JsonProperty("valorPagoModerador")]
        public decimal ValorPagoModerador { get; set; }

        [JsonProperty("numFEVPagoModerador")]
        public string NumeroFacturaPagoModerador { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }

    public class UrgenciaRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("fechaInicioAtencion")]
        public string FechaInicioAtencion { get; set; }

        [JsonProperty("causaMotivoAtencion")]
        public string CausaMotivoAtencion { get; set; }

        [JsonProperty("codDiagnosticoPrincipal")]
        public string CodigoDiagnosticoPrincipal { get; set; }

        [JsonProperty("codDiagnosticoPrincipalE")]
        public string CodigoDiagnosticoPrincipalE { get; set; }

        [JsonProperty("codDiagnosticoRelacionadoE1")]
        public string CodigoDiagnosticoRelacionadoE1 { get; set; }

        [JsonProperty("codDiagnosticoRelacionadoE2")]
        public string CodigoDiagnosticoRelacionadoE2 { get; set; }

        [JsonProperty("codDiagnosticoRelacionadoE3")]
        public string CodigoDiagnosticoRelacionadoE3 { get; set; }

        [JsonProperty("condicionDestinoUsuarioEgreso")]
        public string CondicionDestinoUsuarioEgreso { get; set; }

        [JsonProperty("codDiagnosticoCausaMuerte")]
        public string CodigoDiagnosticoCausaMuerte { get; set; }

        [JsonProperty("fechaEgreso")]
        public string FechaEgreso { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }

    public class HospitalizacionRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("viaIngresoServicioSalud")]
        public string ViaIngresoServicioSalud { get; set; }

        [JsonProperty("fechaInicioAtencion")]
        public string FechaInicioAtencion { get; set; }

        [JsonProperty("numAutorizacion")]
        public string NumeroAutorizacion { get; set; }

        [JsonProperty("causaMotivoAtencion")]
        public string CausaMotivoAtencion { get; set; }

        [JsonProperty("codDiagnosticoPrincipal")]
        public string CodigoDiagnosticoPrincipal { get; set; }

        [JsonProperty("codDiagnosticoPrincipalE")]
        public string CodigoDiagnosticoPrincipalE { get; set; }

        [JsonProperty("codDiagnosticoRelacionadoE1")]
        public string CodigoDiagnosticoRelacionadoE1 { get; set; }

        [JsonProperty("codDiagnosticoRelacionadoE2")]
        public string CodigoDiagnosticoRelacionadoE2 { get; set; }

        [JsonProperty("codDiagnosticoRelacionadoE3")]
        public string CodigoDiagnosticoRelacionadoE3 { get; set; }

        [JsonProperty("codComplicacion")]
        public string CodigoComplicacion { get; set; }

        [JsonProperty("condicionDestinoUsuarioEgreso")]
        public string CondicionDestinoUsuarioEgreso { get; set; }

        [JsonProperty("codDiagnosticoCausaMuerte")]
        public string CodigoDiagnosticoCausaMuerte { get; set; }

        [JsonProperty("fechaEgreso")]
        public string FechaEgreso { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }

    public class RecienNacidoRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; }

        [JsonProperty("numDocumentoIdentificacion")]
        public string NumeroDocumentoIdentificacion { get; set; }

        [JsonProperty("fechaNacimiento")]
        public string FechaNacimiento { get; set; }

        [JsonProperty("edadGestacional")]
        public long EdadGestacional { get; set; }

        [JsonProperty("numConsultasPrenatal")]
        public long NumeroConsultasPrenatal { get; set; }

        [JsonProperty("codSexoBiologico")]
        public string CodigoSexoBiologico { get; set; }

        [JsonProperty("peso")]
        public long Peso { get; set; }

        [JsonProperty("codDiagnosticoPrincipal")]
        public string CodigoDiagnosticoPrincipal { get; set; }

        [JsonProperty("condicionDestinoUsuarioEgreso")]
        public string CondicionDestinoUsuarioEgreso { get; set; }

        [JsonProperty("codDiagnosticoCausaMuerte")]
        public string CodigoDiagnosticoCausaMuerte { get; set; }

        [JsonProperty("fechaEgreso")]
        public string FechaEgreso { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }

    public class MedicamentoRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("numAutorizacion")]
        public string NumeroAutorizacion { get; set; }

        [JsonProperty("idMIPRES")]
        public string IdMipres { get; set; }

        [JsonProperty("fechaDispensAdmon")]
        public string FechaDispensacion { get; set; }

        [JsonProperty("codDiagnosticoPrincipal")]
        public string CodigoDiagnosticoPrincipal { get; set; }

        [JsonProperty("codDiagnosticoRelacionado")]
        public string CodigoDiagnosticoRelacionado { get; set; }

        [JsonProperty("tipoMedicamento")]
        public string TipoMedicamento { get; set; }

        [JsonProperty("codTecnologiaSalud")]
        public string CodigoTecnologiaSalud { get; set; }

        [JsonProperty("nomTecnologiaSalud")]
        public string NombreTecnologiaSalud { get; set; }

        [JsonProperty("concentracionMedicamento")]
        public long ConcentracionMedicamento { get; set; }

        [JsonProperty("unidadMedida")]
        public long UnidadMedida { get; set; }

        [JsonProperty("formaFarmaceutica")]
        public string FormaFarmaceutica { get; set; }

        [JsonProperty("unidadMinDispensa")]
        public long UnidadMinimaDispensada { get; set; }

        [JsonProperty("cantidadMedicamento")]
        public long CantidadMedicamento { get; set; }

        [JsonProperty("diasTratamiento")]
        public long DiasTratamiento { get; set; }

        [JsonProperty("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; }

        [JsonProperty("numDocumentoIdentificacion")]
        public string NumeroDocumentoIdentificacion { get; set; }

        [JsonProperty("vrUnitMedicamento")]
        public decimal ValorUnitario { get; set; }

        [JsonProperty("vrServicio")]
        public decimal ValorTotal { get; set; }

        [JsonProperty("conceptoRecaudo")]
        public string ConceptoRecaudo { get; set; }

        [JsonProperty("valorPagoModerador")]
        public decimal ValorPagoModerador { get; set; }

        [JsonProperty("numFEVPagoModerador")]
        public string NumeroFacturaPagoModerador { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }

    public class OtroServicioRips
    {
        [JsonProperty("codPrestador")]
        public string CodigoPrestador { get; set; }

        [JsonProperty("numAutorizacion")]
        public string NumeroAutorizacion { get; set; }

        [JsonProperty("idMIPRES")]
        public string IdMipres { get; set; }

        [JsonProperty("fechaSuministroTecnologia")]
        public string FechaSuministroTecnologia { get; set; }

        [JsonProperty("tipoOS")]
        public string Tipo { get; set; }

        [JsonProperty("codTecnologiaSalud")]
        public string CodigoTecnologiaSalud { get; set; }

        [JsonProperty("nomTecnologiaSalud")]
        public string NombreTecnologiaSalud { get; set; }

        [JsonProperty("cantidadOS ")]
        public long Cantidad { get; set; }

        [JsonProperty("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; }

        [JsonProperty("numDocumentoIdentificacion")]
        public string NumeroDocumentoIdentificacion { get; set; }

        [JsonProperty("vrUnitOS")]
        public decimal ValorUnitario { get; set; }

        [JsonProperty("vrServicio")]
        public decimal ValorTotal { get; set; }

        [JsonProperty("conceptoRecaudo")]
        public string ConceptoRecaudo { get; set; }

        [JsonProperty("valorPagoModerador")]
        public decimal ValorPagoModerador { get; set; }

        [JsonProperty("numFEVPagoModerador")]
        public string NumeroFacturaPagoModerador { get; set; }

        [JsonProperty("consecutivo")]
        public long Consecutivo { get; set; }
    }
}