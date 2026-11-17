using FacturacionColombia_Metadatos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionTicketExpectaculoPublico : ExtensionDocumentoEquivalente
    {
        public class Productor
        {
            /// <summary>
            /// Apellidos y nombre o razón social del
            /// productor u organizador del espectáculo
            /// público
            /// </summary>
            public string Nombre { get; set; }

            /// <summary>
            /// NIT del productor u organizador del
            /// espectáculo público
            /// </summary>
            public string Nit { get; set; }

            /// <summary>
            /// Código LEP de productor de espectáculos
            /// públicos de acuerdo a su categoría.
            /// </summary>
            public string CodigoLep { get; set; }
        }

        public class Evento
        {
            /// <summary>
            /// Nombre del espectáculo público
            /// </summary>
            public string Nombre { get; set; }

            /// <summary>
            /// Sala, escenario, puesto y/o localidad asignada
            /// </summary>
            public string Localidad { get; set; }

            /// <summary>
            /// Código único del evento – PULEP
            /// </summary>
            public string CodigoPulep { get; set; }

            /// <summary>
            /// Indicar el tipo de venta:
            /// Contener Literal “Invididual” o “Grupal”
            /// </summary>
            public string TipoTicket { get; set; }

            /// <summary>
            /// Etapa de la venta
            /// </summary>
            public string EtapaVenta { get; set; }

            /// <summary>
            /// Indicar la categoría del evento
            /// </summary>
            public string Categoria { get; set; }

            /// <summary>
            /// Departamento donde se realizará el evento
            /// </summary>
            public Departamento Departamento { get; set; }

            /// <summary>
            /// Ciudad o Municipio donde se realizará el evento.
            /// </summary>
            public Municipio Muncipio { get; set; }

            /// <summary>
            /// Nombre del sitio, dirección o ublicación
            /// donde se realizará el evento
            /// </summary>
            public string Lugar { get; set; }

            /// <summary>
            /// Fecha del evento
            /// </summary>
            public DateTime Fecha { get; set; }
        }

        public class Informacion
        {
            /// <summary>
            /// Información sobre el productor u organizador
            /// del espectáculo público
            /// </summary>
            public Productor Productor { get; set; }

            /// <summary>
            /// Grupo para informar datos relacionados con
            /// el espectáculo público
            /// </summary>
            public Evento Evento { get; set; }
        }

        private Informacion informacion;

        public void ConInformacion(Informacion informacion)
        {
            this.informacion = informacion;
        }

        public override XmlElement GetExtensionContent()
        {
            var docXml = new XmlDocument();
            docXml.LoadXml($@"<PublicShowsInformation>
    <Producer>
	    <ProducerName>{informacion.Productor.Nombre}</ProducerName>
	    <ProducerNIT>{informacion.Productor.Nit}</ProducerNIT>
        <LEPCode>{informacion.Productor.CodigoLep}</LEPCode>
    </Producer>
    <EventInformation>
	    <EventName>{informacion.Evento.Nombre}</EventName>
	    <TicketLocation>{informacion.Evento.Localidad}</TicketLocation>
	    <PULEPCode>{informacion.Evento.CodigoPulep}</PULEPCode>
	    <TicketType>{informacion.Evento.TipoTicket}</TicketType>
	    <SalePhase>{informacion.Evento.EtapaVenta}</SalePhase>
	    <EventCategory>{informacion.Evento.Categoria}</EventCategory>
	    <CountrySubentity>{informacion.Evento.Departamento.Nombre}</CountrySubentity>
	    <CountrySubentityCode>{informacion.Evento.Departamento.Codigo}</CountrySubentityCode>
	    <CityName>{informacion.Evento.Muncipio.Nombre}</CityName>
	    <ID>{informacion.Evento.Muncipio.Codigo}</ID>
	    <EventLocation>{informacion.Evento.Lugar}</EventLocation>
	    <DateEvent>{informacion.Evento.Fecha.ToString("yyyy-MM-dd")}</DateEvent>
	    <TimeEvent>{informacion.Evento.Fecha.ToString("HH:mm:ss")}</TimeEvent>
    </EventInformation>
</PublicShowsInformation>");

            return docXml.DocumentElement;
        }
    }
}
