using System;

namespace NominaElectronica.MetaDatos
{
    [Serializable]
    public class MetadatosValorNoPermitdoException: Exception
    {
        //public string StudentName { get; }

        public MetadatosValorNoPermitdoException() { }

        public MetadatosValorNoPermitdoException(string message): base(message) { }

        //public MetadatosValorNoPermitdoException(string message, Exception inner)
        //    : base(message, inner) { }

        //public MetadatosValorNoPermitdoException(string message, string studentName)
        //    : this(message)
        //{
        //    StudentName = studentName;
        //}
    }
}
