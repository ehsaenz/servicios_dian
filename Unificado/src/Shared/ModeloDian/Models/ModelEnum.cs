
namespace FE.Enum
{
    // Enumeraciones usadas por el servicio
    public enum Ambito { NONE = 0, ERP = 1, WEBSERVICE = 2, DIAN = 3 }

    // Enumeraciones usadas en DocumentoDian 
    public enum TipoPersona { JURIDICA = 1, NATURAL = 2 }
    public enum TipoOperacion { SUMA = 1, RESTA = 2 }
    public enum TipoCalculo { PORCENTUAL = 1, NOMINAL = 2, MILESIMAL = 3}
    public enum TipoDocEnum : byte { FacturaVenta = 1, FacturaExportacion = 2, FacturaContingencia = 3, NotaCredito = 91, NotaDebito = 92 }

    public enum MetodoPagoEnum : byte { Contado = 1, Credito = 2}

    public enum enmModo { NORMAL = 0, CONTINGENCIA = 1 }


}

