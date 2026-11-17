using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using eFacturacionColombia_NETCore.Tipos.Standard;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public static class GeneradorExtensions
    {
        public static GeneradorFactura AgregarExtension(this GeneradorFactura generador, BaseExtensionSector extension)
        {
            return InsertarExtension(generador, generador.Invoice.UBLExtensions.Length, extension);
        }

        public static GeneradorFactura InsertarExtension(this GeneradorFactura generador, int index, BaseExtensionSector extension)
        {
            extension.RootNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

            var extensions = generador.Invoice.UBLExtensions.ToList();

            var ublExtension = new UBLExtensionType
            {
                ExtensionContent = new ExtensionContentType
                {
                    Any = extension.SerializeXmlElement()
                }
            };

            extensions.Insert(index, ublExtension);

            generador.Invoice.UBLExtensions = extensions.ToArray();

            return generador;
        }


        public static GeneradorNotaCredito AgregarExtension(this GeneradorNotaCredito generador, BaseExtensionSector extension)
        {
            return InsertarExtension(generador, generador.CreditNote.UBLExtensions.Length, extension);
        }

        public static GeneradorNotaCredito InsertarExtension(this GeneradorNotaCredito generador, int index, BaseExtensionSector extension)
        {
            extension.RootNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

            var extensions = generador.CreditNote.UBLExtensions.ToList();

            var ublExtension = new UBLExtensionType
            {
                ExtensionContent = new ExtensionContentType
                {
                    Any = extension.SerializeXmlElement()
                }
            };

            extensions.Insert(index, ublExtension);

            generador.CreditNote.UBLExtensions = extensions.ToArray();

            return generador;
        }


        public static GeneradorNotaDebito AgregarExtension(this GeneradorNotaDebito generador, BaseExtensionSector extension)
        {
            return InsertarExtension(generador, generador.DebitNote.UBLExtensions.Length, extension);
        }

        public static GeneradorNotaDebito InsertarExtension(this GeneradorNotaDebito generador, int index, BaseExtensionSector extension)
        {
            extension.RootNamespace = "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2";

            var extensions = generador.DebitNote.UBLExtensions.ToList();

            var ublExtension = new UBLExtensionType
            {
                ExtensionContent = new ExtensionContentType
                {
                    Any = extension.SerializeXmlElement()
                }
            };

            extensions.Insert(index, ublExtension);

            generador.DebitNote.UBLExtensions = extensions.ToArray();

            return generador;
        }


        public static GeneradorDocumentoEquivalente AgregarExtension (this GeneradorDocumentoEquivalente generador, ExtensionDocumentoEquivalente extension)
        {
            return InsertarExtension(generador, generador.Invoice.UBLExtensions.Length, extension);
        }

        public static GeneradorDocumentoEquivalente InsertarExtension(this GeneradorDocumentoEquivalente generador, int index, ExtensionDocumentoEquivalente extension)
        {
            var extensions = generador.Invoice.UBLExtensions.ToList();

            var ublExtension = new UBLExtensionType
            {
                ExtensionContent = new ExtensionContentType
                {
                    Any = extension.GetExtensionContent()
                }
            };

            extensions.Insert(index, ublExtension);

            generador.Invoice.UBLExtensions = extensions.ToArray();

            return generador;
        }

    }
}