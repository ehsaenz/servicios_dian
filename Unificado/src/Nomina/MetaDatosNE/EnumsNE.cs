using System;

namespace NominaElectronica.Enums
{
    public enum enmAmbiente { Produccion = 1, Pruebas = 2};
    public enum enmRolPersona { Empleador = 0, Empleado = 1 };
    public enum enmTipoDocElec { NominaElectronicaIndividual = 102, NominaElectronicaIndividualAjuste = 103 }
    public enum enmTipoEnvio { Normal = 0, Reemplazar = 1, Eliminar = 2 }
    public enum enmEstadoEnvio { PENDIENTE = 0, ENVIADO = 1, APROBADO = 2, RECHAZADO = 3, ELIMINADO = 4, REEMPLAZADO = 5 }
    public enum enmTipoIncapacidad { COMUN = 1, PROFESIONALO = 2, LABORAL = 3 }
    public enum enmTipoIngreso { CONSTITUTIVO_SALARIO = 0, NO_CONSTITUTIVO_SALARIO = 1 } 
    public enum enmConceptoIngreso { BONO_ALIMENTACION = 0, BONO_GRAL = 1, BONIFICACION = 2, AUXILIO = 3, COMISION = 4}
}
