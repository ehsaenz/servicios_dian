using FacturacionColombia_Metadatos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionJuegoLocalizado : ExtensionDocumentoEquivalente
    {
        public class Juego
        {
            /// <summary>
            /// Tipo de juego
            /// Indicar alguno de los literales “MET”, “Mesa” o “Bingo”
            /// </summary>
            public string Tipo { get; set; }

            /// <summary>
            /// Número de instrumentos de del tipo de juego
            /// informado en el TypeOfGame
            /// </summary>
            public int NumeroInstrumentos { get; set; }

            /// <summary>
            /// Número del serial del instrumento de juego
            /// con reporte diario crédito
            /// </summary>
            public string NumeroSerialMaquina { get; set; }

            /// <summary>
            /// Informar el valor crédito de un instrumento
            /// de juego, cuando el elemento
            /// Invoice/cac:InvoiceLine/cbc:LineExtensionAmount es cero(0)
            /// </summary>
            public decimal ValorCredito { get; set; }

            public Moneda Moneda { get; set; } = Moneda.PESO_COLOMBIANO;
        }

        public class Informacion
        {
            /// <summary>
            /// Dirección de establecimiento o dirección de
            /// internet e IP cuando es virtual
            /// </summary>
            public string Establecimiento { get; set; }

            /// <summary>
            /// Valor de la base o depósito de dinero
            /// </summary>
            public decimal Deposito { get; set; }

            /// <summary>
            /// Grupo de información específica de los tipos
            /// de juegos localizados reportados por parte
            /// de la sala o punto de juego.
            /// </summary>
            public Juego Juego { get; set; }
        }

        private Informacion informacion;

        public void ConInformacion(Informacion informacion)
        {
            this.informacion = informacion;
        }

        public override XmlElement GetExtensionContent()
        {
            var docXml = new XmlDocument();
            docXml.LoadXml($@"<InfoEstablishment>
    <InfoPoint>
	    <EstablishmentAddress>{informacion.Establecimiento}</EstablishmentAddress>
        <MoneyDeposit>{informacion.Deposito}</MoneyDeposit>
    </InfoPoint>
	<GameInformation>
	    <TypeOfGame>{informacion.Juego.Tipo}</TypeOfGame>
	    <GameTypeTotal>{informacion.Juego.NumeroInstrumentos}</GameTypeTotal>
        <CreditValueOfTheMachine>
            <GameMachineSerial>{informacion.Juego.NumeroSerialMaquina}</GameMachineSerial>
            <CreditValue currencyId=""{informacion.Juego.Moneda.Codigo}"">{informacion.Juego.ValorCredito}</CreditValue>
        </CreditValueOfTheMachine>
    </GameInformation>
</InfoEstablishment>");

            return docXml.DocumentElement;
        }
    }
}
