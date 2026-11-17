using System;
using System.Collections.Generic;
using System.Linq;
using DIAN.Tenants;

namespace DIAN.Admin.Web
{
    public enum ResultadoItem
    {
        Cumplido,

        /// <summary>No impide pasar a producción, pero conviene resolverlo.</summary>
        Aviso,

        /// <summary>Impide autorizar producción.</summary>
        Pendiente
    }

    public sealed record ItemProduccion(string Titulo, ResultadoItem Resultado, string Detalle);

    /// <summary>
    /// Lista de verificación para pasar un cliente de habilitación a producción. Es solo lectura: no cambia nada, solo dice
    /// qué falta. La autorización sigue exigiendo la frase de confirmación del operador.
    /// </summary>
    public static class ListaProduccion
    {
        private const string TipoFactura = "01";
        private const string TipoNotaCredito = "91";
        private const string TipoNotaDebito = "92";

        public static IReadOnlyList<ItemProduccion> Evaluar(ClienteRegistro c, IReadOnlyList<DocumentoVista> documentos, DateTime ahoraUtc)
        {
            var items = new List<ItemProduccion>();

            // 1. Emisor
            var faltanBloqueantes = new List<string>();
            if (string.IsNullOrWhiteSpace(c.Emisor.Nit)) faltanBloqueantes.Add("NIT");
            if (string.IsNullOrWhiteSpace(c.Emisor.DV)) faltanBloqueantes.Add("DV");
            if (string.IsNullOrWhiteSpace(c.Emisor.RazonSocial)) faltanBloqueantes.Add("razón social");
            var faltanOpcionales = new List<string>();
            if (string.IsNullOrWhiteSpace(c.Emisor.MatriculaMercantil)) faltanOpcionales.Add("matrícula mercantil");
            if (string.IsNullOrWhiteSpace(c.Emisor.Email)) faltanOpcionales.Add("correo");
            if (string.IsNullOrWhiteSpace(c.Emisor.NumeroTel)) faltanOpcionales.Add("teléfono");
            items.Add(faltanBloqueantes.Count > 0
                ? new ItemProduccion("Datos del emisor", ResultadoItem.Pendiente, "Falta: " + string.Join(", ", faltanBloqueantes) + ".")
                : faltanOpcionales.Count > 0
                    ? new ItemProduccion("Datos del emisor", ResultadoItem.Aviso, "Sin " + string.Join(", ", faltanOpcionales) + " (salen en la representación gráfica).")
                    : new ItemProduccion("Datos del emisor", ResultadoItem.Cumplido, c.Emisor.RazonSocial));

            // 2. Certificado
            if (string.IsNullOrEmpty(c.ArchivoCertificado))
            {
                items.Add(new ItemProduccion("Certificado de firma", ResultadoItem.Pendiente, "No se ha cargado el .p12."));
            }
            else if (c.CertificadoVenceUtc is { } vence && vence <= ahoraUtc)
            {
                items.Add(new ItemProduccion("Certificado de firma", ResultadoItem.Pendiente, "Venció el " + vence.ToString("yyyy-MM-dd") + "."));
            }
            else if (c.CertificadoVenceUtc is { } pronto && pronto < ahoraUtc.AddDays(30))
            {
                items.Add(new ItemProduccion("Certificado de firma", ResultadoItem.Aviso, "Vence pronto: " + pronto.ToString("yyyy-MM-dd") + "."));
            }
            else
            {
                items.Add(new ItemProduccion("Certificado de firma", ResultadoItem.Cumplido, "Vigente hasta " + c.CertificadoVenceUtc?.ToString("yyyy-MM-dd") + "."));
            }

            // 3. Software de facturación electrónica: habilitado y luego el de producción
            var fe = c.Software.Where(s => s.TipoSoftware == TipoSoftware.FacturacionElectronica).ToList();
            items.Add(fe.Count == 0
                ? new ItemProduccion("Software de facturación electrónica", ResultadoItem.Pendiente, "Falta registrar el software (identificador y PIN).")
                : new ItemProduccion("Software de facturación electrónica", ResultadoItem.Cumplido, "Registrado."));

            // 4. Set de pruebas: documentos aprobados por la DIAN de habilitación, por tipo
            int Aprobados(string tipo) => documentos.Count(d => d.Estado == 1 && d.Habilitacion && d.TipoDocDian == tipo);
            var fv = Aprobados(TipoFactura);
            var nc = Aprobados(TipoNotaCredito);
            var nd = Aprobados(TipoNotaDebito);
            var resumen = $"Aprobados en habilitación: {fv} factura(s), {nc} nota(s) crédito, {nd} nota(s) débito.";
            items.Add(fv == 0
                ? new ItemProduccion("Set de pruebas de la DIAN", ResultadoItem.Pendiente, "Ninguna factura aprobada en habilitación todavía. " + resumen)
                : nc == 0 || nd == 0
                    ? new ItemProduccion("Set de pruebas de la DIAN", ResultadoItem.Aviso, "Faltan notas por probar. " + resumen)
                    : new ItemProduccion("Set de pruebas de la DIAN", ResultadoItem.Cumplido, resumen));

            // 5. Software marcado como producción (con la PIN y el identificador de producción)
            var enProduccion = fe.Any(s => !s.AmbientePruebas);
            items.Add(enProduccion
                ? new ItemProduccion("Software de producción", ResultadoItem.Cumplido, "El software de facturación está marcado como producción.")
                : new ItemProduccion("Software de producción", ResultadoItem.Pendiente,
                    "Registre el software de producción (identificador y PIN de producción) y desmarque «Ambiente de pruebas»."));

            // 6. Clave de API activa
            items.Add(c.Claves.Any(k => k.Activa)
                ? new ItemProduccion("Clave de API", ResultadoItem.Cumplido, "Tiene al menos una clave activa.")
                : new ItemProduccion("Clave de API", ResultadoItem.Aviso, "Aún no tiene clave de API: su ERP no podrá enviar."));

            // 7. Logo
            items.Add(!string.IsNullOrEmpty(c.ArchivoLogo) || c.Sucursales.Any(s => !string.IsNullOrEmpty(s.ArchivoLogo))
                ? new ItemProduccion("Logo", ResultadoItem.Cumplido, "Cargado.")
                : new ItemProduccion("Logo", ResultadoItem.Aviso, "Sin logo: el PDF sale sin él."));

            return items;
        }

        /// <summary>Mensaje de lo que impide autorizar producción, o null si no hay impedimentos.</summary>
        public static string? Impedimentos(IReadOnlyList<ItemProduccion> items)
        {
            var pendientes = items.Where(i => i.Resultado == ResultadoItem.Pendiente).Select(i => i.Titulo).ToList();
            return pendientes.Count == 0 ? null : "Aún falta: " + string.Join("; ", pendientes) + ".";
        }
    }
}
