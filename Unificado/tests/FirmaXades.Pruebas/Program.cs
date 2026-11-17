using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using FirmaXadesNETCore;
using FirmaXadesNETCore.Crypto;
using FirmaXadesNETCore.Signature.Parameters;

// Herramienta de prueba de firma XAdES. El mismo archivo se compila contra la copia nueva
// (.NET 10, Unificado\src\Shared) y contra la copia original (.NET 6) para comparar resultados.
//
// Uso:
//   certificado <salida.pfx>                          crea un certificado autofirmado de PRUEBA
//   firmar <entrada.xml> <cert.pfx> <salida.xml>      firma como lo hace FirmaElectronica (factura)
//   validar <firmado.xml>                             valida la firma con XadesService
//   estructura <firmado.xml>                          imprime la estructura sin valores variables
//   verificar <firmado.xml>                           verificación independiente con SignedXml
//   certificado-info <cert.pfx>                       revisa en local si un certificado sirve para firmar
// Para un certificado real, la clave va en la variable de entorno FIRMA_PFX_CLAVE.
namespace FirmaXades.Pruebas
{
    public static class Program
    {
        // Certificado y clave son solo de prueba; no son de ningún cliente.
        private const string ClavePfx = "prueba";

        // Fecha fija para que la parte de la firma que depende de la hora sea comparable.
        private static readonly DateTime FechaFija = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Unspecified);

        public static int Main(string[] args)
        {
            try
            {
                switch (args.Length > 0 ? args[0] : "")
                {
                    case "certificado": return Certificado(args[1]);
                    case "firmar": return Firmar(args[1], args[2], args[3]);
                    case "validar": return Validar(args[1]);
                    case "estructura": return Estructura(args[1]);
                    case "verificar": return VerificarXmldsig(args[1]);
                    case "certificado-info": return CertificadoInfo(args[1]);
                    default:
                        Console.Error.WriteLine("uso: certificado <pfx> | firmar <xml> <pfx> <salida> | validar <firmado> | estructura <firmado>");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static X509Certificate2 CargarPfx(string ruta)
        {
            // Con un certificado real, la clave se toma de la variable de entorno FIRMA_PFX_CLAVE.
            // Nunca se pasa como argumento ni se imprime.
            var clave = Environment.GetEnvironmentVariable("FIRMA_PFX_CLAVE") ?? ClavePfx;
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12FromFile(ruta, clave, X509KeyStorageFlags.EphemeralKeySet);
#else
            return new X509Certificate2(ruta, clave, X509KeyStorageFlags.EphemeralKeySet);
#endif
        }

        // Revisa, solo en local y sin enviar nada, si un certificado sirve para firmar con esta biblioteca.
        private static int CertificadoInfo(string ruta)
        {
            using (var cert = CargarPfx(ruta))
            {
                Console.WriteLine("Sujeto:        " + cert.Subject);
                Console.WriteLine("Emisor:        " + cert.Issuer);
                Console.WriteLine("Vigencia:      " + cert.NotBefore.ToString("yyyy-MM-dd") + " a " + cert.NotAfter.ToString("yyyy-MM-dd")
                    + " (" + (int)(cert.NotAfter - DateTime.Now).TotalDays + " días restantes)");
                Console.WriteLine("Algoritmo:     " + cert.SignatureAlgorithm.FriendlyName);
                Console.WriteLine("Clave privada: " + (cert.HasPrivateKey ? "sí" : "NO"));

                using (var rsa = cert.GetRSAPrivateKey())
                {
                    if (rsa == null)
                    {
                        Console.WriteLine("RSA:           no disponible (GetRSAPrivateKey devolvió null)");
                    }
                    else
                    {
                        Console.WriteLine("RSA:           " + rsa.KeySize + " bits, " + rsa.GetType().Name);
                        try
                        {
                            var datos = System.Text.Encoding.UTF8.GetBytes("prueba");
                            var firma = rsa.SignData(datos, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            Console.WriteLine("Firma SHA-256: correcta (" + firma.Length + " bytes)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Firma SHA-256: FALLA (" + ex.GetType().Name + "): " + ex.Message);
                        }
                    }
                }

                foreach (var uso in cert.Extensions.OfType<X509KeyUsageExtension>())
                {
                    Console.WriteLine("Uso de clave:  " + uso.KeyUsages);
                }

                using (var cadena = new X509Chain())
                {
                    cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    var valida = cadena.Build(cert);
                    Console.WriteLine("Cadena:        " + (valida ? "válida" : "con observaciones") + " (" + cadena.ChainElements.Count + " certificados)");
                    foreach (var estado in cadena.ChainStatus)
                    {
                        Console.WriteLine("  - " + estado.Status + ": " + (estado.StatusInformation ?? "").Trim());
                    }
                }
            }
            return 0;
        }

        private static int Certificado(string salida)
        {
            using (var rsa = RSA.Create(2048))
            {
                var req = new CertificateRequest("CN=Prueba FirmaXades, O=Certificado de prueba", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

                using (var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2)))
                {
                    File.WriteAllBytes(salida, cert.Export(X509ContentType.Pfx, ClavePfx));
                }
            }
            Console.WriteLine("certificado de prueba creado: " + salida);
            return 0;
        }

        // Replica FirmaElectronica.FirmarDocumento para una factura (UBL Invoice).
        private static int Firmar(string entrada, string rutaPfx, string salida)
        {
            var xpath = new SignatureXPathExpression();
            xpath.Namespaces.Add("fe", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            xpath.Namespaces.Add("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            xpath.Namespaces.Add("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
            xpath.Namespaces.Add("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
            xpath.Namespaces.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            xpath.Namespaces.Add("sts", "dian:gov:co:facturaelectronica:Structures-2-1");
            xpath.Namespaces.Add("xades", "http://uri.etsi.org/01903/v1.3.2#");
            xpath.Namespaces.Add("xades141", "http://uri.etsi.org/01903/v1.4.1#");
            xpath.XPathExpression = "/fe:Invoice/ext:UBLExtensions/ext:UBLExtension[2]/ext:ExtensionContent";

            var parametros = new SignatureParameters();
            parametros.SignatureMethod = SignatureMethod.RSAwithSHA256;
            parametros.DigestMethod = DigestMethod.SHA256;
            parametros.SigningDate = FechaFija;
            parametros.SignerRole = new SignerRole();
            parametros.SignerRole.ClaimedRoles.Add("supplier");
            parametros.SignatureDestination = xpath;
            parametros.SignaturePolicyInfo = new SignaturePolicyInfo();
            parametros.SignaturePolicyInfo.PolicyIdentifier = "https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf";
            parametros.SignaturePolicyInfo.PolicyHash = "dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=";
            parametros.SignaturePolicyInfo.PolicyDigestAlgorithm = DigestMethod.SHA256;
            parametros.SignaturePackaging = SignaturePackaging.ENVELOPED;
            parametros.DataFormat = new DataFormat { MimeType = "text/xml" };

            var servicio = new XadesService();
            using (var certificado = CargarPfx(rutaPfx))
            using (parametros.Signer = new Signer(certificado))
            using (var stream = new MemoryStream(File.ReadAllBytes(entrada)))
            {
                var documento = servicio.Sign(stream, parametros);
                using (var salidaStream = new FileStream(salida, FileMode.Create))
                {
                    documento.Save(salidaStream);
                }
            }
            Console.WriteLine("firmado: " + salida);
            return 0;
        }

        private static int Validar(string firmado)
        {
            var servicio = new XadesService();
            var documentos = servicio.Load(firmado);
            // XadesValidator descarta la excepción real; se repite la comprobación para ver la causa.
            try
            {
                documentos[0].XadesSignature.CheckXmldsigSignature();
                Console.WriteLine("xmldsig: correcta");
            }
            catch (Exception ex)
            {
                var traza = (ex.StackTrace ?? "").Split('\n').Take(4);
                Console.WriteLine("xmldsig: falla (" + ex.GetType().Name + "): " + ex.Message + Environment.NewLine + string.Join(Environment.NewLine, traza));
            }
            var resultado = servicio.Validate(documentos[0]);
            Console.WriteLine("valido=" + resultado.IsValid + " mensaje=" + resultado.Message);
            return resultado.IsValid ? 0 : 3;
        }

        // Verificación independiente con System.Security.Cryptography.Xml (no usa Microsoft.Xades):
        // comprueba las huellas de todas las referencias y la firma con la clave pública del certificado incluido.
        // Es la comprobación que vale como prueba, porque XadesValidator depende de tipos internos de
        // .NET Framework y falla igual con la copia original (ver docs\fase1-bitacora-migracion.md).
        private static int VerificarXmldsig(string firmado)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(firmado);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            var nodos = doc.SelectNodes("//ds:Signature", ns);
            if (nodos == null || nodos.Count != 1)
            {
                Console.WriteLine("xmldsig estándar: se esperaba 1 firma y hay " + (nodos == null ? 0 : nodos.Count));
                return 3;
            }

            var firma = new System.Security.Cryptography.Xml.SignedXml(doc);
            firma.LoadXml((XmlElement)nodos[0]);
            var referencias = firma.SignedInfo.References.Count;
            var correcta = firma.CheckSignature();
            Console.WriteLine("xmldsig estándar: " + (correcta ? "correcta" : "INCORRECTA") + " (referencias firmadas: " + referencias + ")");
            return correcta ? 0 : 3;
        }

        // Imprime elementos y atributos, sin Ids, URIs ni valores de firma, que cambian en cada ejecución.
        private static int Estructura(string firmado)
        {
            var xml = new XmlDocument { PreserveWhitespace = true };
            xml.Load(firmado);
            Recorrer(xml.DocumentElement, "");
            return 0;
        }

        private static readonly string[] AtributosConValor = { "Algorithm", "Type", "MimeType", "Encoding" };
        private static readonly string[] ElementosConTexto = { "SigningTime", "Identifier", "ClaimedRole", "Description" };

        private static void Recorrer(XmlElement elemento, string ruta)
        {
            var actual = ruta + "/" + elemento.Name;

            var atributos = elemento.Attributes.Cast<XmlAttribute>()
                .Where(a => !a.Name.StartsWith("xmlns"))
                .OrderBy(a => a.Name)
                .Select(a => AtributosConValor.Contains(a.Name) ? a.Name + "=" + a.Value : a.Name);

            var texto = "";
            var esHoja = !elemento.ChildNodes.OfType<XmlElement>().Any();
            if (esHoja && ElementosConTexto.Contains(elemento.LocalName))
            {
                texto = " = " + elemento.InnerText;
            }
            if (esHoja && elemento.LocalName == "DigestValue" && elemento.ParentNode != null && elemento.ParentNode.ParentNode != null
                && elemento.ParentNode.ParentNode.LocalName == "SigPolicyHash")
            {
                texto = " = " + elemento.InnerText;
            }

            Console.WriteLine(actual + " [" + string.Join(",", atributos) + "]" + texto);

            foreach (var hijo in elemento.ChildNodes.OfType<XmlElement>())
            {
                Recorrer(hijo, actual);
            }
        }
    }
}
