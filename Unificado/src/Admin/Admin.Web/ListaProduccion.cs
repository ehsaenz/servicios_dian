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

        public static IReadOnlyList<ItemProduccion> Evaluar(
            ClienteRegistro c,
            IReadOnlyList<DocumentoVista> documentos,
            DateTime ahoraUtc,
            EvidenciaSets? sets = null)
        {
            sets ??= new EvidenciaSets();
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

            // Cada bloque (facturación, POS, nómina, RADIAN) se habilita por separado y tiene su propio set de pruebas.
            // Un bloque en habilitación no bloquea. Un bloque en producción bloquea hasta que su set esté verificado.
            var modulos = c.Software.ToList();
            var fe = modulos.Where(s => s.TipoSoftware == TipoSoftware.FacturacionElectronica).ToList();
            var feEnProduccion = fe.Any(s => !s.AmbientePruebas);

            if (modulos.Count == 0)
            {
                items.Add(new ItemProduccion("Software de facturación electrónica", ResultadoItem.Pendiente, "Falta registrar el software (identificador y PIN)."));
            }
            else if (fe.Count > 0)
            {
                items.Add(new ItemProduccion("Software de facturación electrónica", ResultadoItem.Cumplido, feEnProduccion ? "Registrado en producción." : "Registrado en habilitación."));
            }

            // 3. Facturación electrónica (01, 91, 92): única fuente bloqueante es el estado oficial del set DIAN;
            // los conteos locales quedan como referencia informativa.
            int Aprobados(string tipo) => documentos.Count(d => d.Estado == 1 && d.Habilitacion && d.TipoDocDian == tipo);
            var fv = Aprobados(TipoFactura);
            var nc = Aprobados(TipoNotaCredito);
            var nd = Aprobados(TipoNotaDebito);
            var resumen = $"Aprobados en habilitación (informativo): {fv} factura(s), {nc} nota(s) crédito, {nd} nota(s) débito.";
            var setFe = sets.SetDe(TipoSoftware.FacturacionElectronica);
            if (fe.Count > 0 || modulos.Count == 0)
            {
                items.Add(EvaluarEstadoSet("Set de pruebas de la DIAN", feEnProduccion || modulos.Count == 0, setFe, resumen));
            }

            // 4. Resto de bloques (POS, nómina, RADIAN, documento soporte): cada uno con su propio set de pruebas.
            foreach (var s in modulos.Where(s => s.TipoSoftware != TipoSoftware.FacturacionElectronica))
            {
                var nombre = NombreModulo(s.TipoSoftware);
                items.Add(new ItemProduccion("Software de " + nombre, ResultadoItem.Cumplido,
                    s.AmbientePruebas ? "Registrado en habilitación." : "Registrado en producción."));
                items.Add(EvaluarModulo(s, nombre, sets));
            }

            // 5. Clave de API activa
            items.Add(c.Claves.Any(k => k.Activa)
                ? new ItemProduccion("Clave de API", ResultadoItem.Cumplido, "Tiene al menos una clave activa.")
                : new ItemProduccion("Clave de API", ResultadoItem.Aviso, "Aún no tiene clave de API: su ERP no podrá enviar."));

            // 6. Logo
            items.Add(!string.IsNullOrEmpty(c.ArchivoLogo) || c.Sucursales.Any(s => !string.IsNullOrEmpty(s.ArchivoLogo))
                ? new ItemProduccion("Logo", ResultadoItem.Cumplido, "Cargado.")
                : new ItemProduccion("Logo", ResultadoItem.Aviso, "Sin logo: el PDF sale sin él."));

            return items;
        }

        private static ItemProduccion EvaluarModulo(SoftwareRegistro s, string nombre, EvidenciaSets sets)
        {
            var titulo = "Set de pruebas de " + nombre;
            var setBloque = sets.SetDe(s.TipoSoftware);
            var bloquea = !s.AmbientePruebas;

            switch (s.TipoSoftware)
            {
                case TipoSoftware.DocumentoEquivalente:
                {
                    var resumen = $"Aprobados en habilitación (informativo): {sets.TiquetesPosAprobados} tiquete(s) POS, {sets.NotasAjustePosAprobadas} nota(s) de ajuste.";
                    return EvaluarEstadoSet(titulo, bloquea, setBloque, resumen);
                }
                case TipoSoftware.DocumentoSoporte:
                {
                    var resumen = $"Aprobados en habilitación (informativo): {sets.DocumentosSoporteAprobados} documento(s) soporte.";
                    return EvaluarEstadoSet(titulo, bloquea, setBloque, resumen);
                }
                case TipoSoftware.NominaElectronica:
                {
                    var resumen = $"Aprobados en habilitación (informativo): {sets.NominasIndividualesAprobadas} nómina(s) individual(es), {sets.NominasAjusteAprobadas} nota(s) de ajuste.";
                    return EvaluarEstadoSet(titulo, bloquea, setBloque, resumen);
                }
                case TipoSoftware.EventosRadian:
                {
                    var resumen = $"Aprobados en habilitación (informativo): {sets.EventosRadianAprobados} evento(s) RADIAN.";
                    return EvaluarEstadoSet(titulo, bloquea, setBloque, resumen);
                }
                default:
                    return EvaluarEstadoSet(titulo, bloquea, setBloque, "Módulo sin conteos locales definidos.");
            }
        }

        private static ItemProduccion EvaluarEstadoSet(string titulo, bool bloqueaSiNoAceptado, EstadoSetBloque? setBloque, string resumenInformativo)
        {
            if (setBloque is { Aceptado: true })
            {
                var detalleSet = string.IsNullOrWhiteSpace(setBloque.MensajeVisible)
                    ? "La DIAN informó que el set de pruebas se encuentra aceptado."
                    : setBloque.MensajeVisible;
                return new ItemProduccion(titulo, ResultadoItem.Cumplido, $"{detalleSet} {resumenInformativo}");
            }

            var resultado = bloqueaSiNoAceptado ? ResultadoItem.Pendiente : ResultadoItem.Aviso;
            if (setBloque != null && (!string.IsNullOrWhiteSpace(setBloque.MensajeVisible) || !string.IsNullOrWhiteSpace(setBloque.Estado)))
            {
                var prefijo = !string.IsNullOrWhiteSpace(setBloque.Estado)
                    ? $"Estado del set: {setBloque.Estado}."
                    : "Set de pruebas aún sin aceptar por la DIAN.";
                var detalle = string.IsNullOrWhiteSpace(setBloque.MensajeVisible) ? prefijo : $"{prefijo} {setBloque.MensajeVisible}";
                return new ItemProduccion(titulo, resultado, $"{detalle} {resumenInformativo}");
            }

            return new ItemProduccion(titulo, resultado, $"Set de pruebas aún sin confirmación de aceptación en la DIAN. {resumenInformativo}");
        }

        private static string NombreModulo(TipoSoftware tipo) => tipo switch
        {
            TipoSoftware.FacturacionElectronica => "facturación electrónica",
            TipoSoftware.NominaElectronica => "nómina electrónica",
            TipoSoftware.EventosRadian => "eventos RADIAN",
            TipoSoftware.DocumentoSoporte => "documento soporte",
            TipoSoftware.DocumentoEquivalente => "documento equivalente (POS)",
            _ => tipo.ToString()
        };

        public static string? Impedimentos(IReadOnlyList<ItemProduccion> items)
        {
            var pendientes = items.Where(i => i.Resultado == ResultadoItem.Pendiente).Select(i => i.Titulo).ToList();
            return pendientes.Count == 0 ? null : "Aún falta: " + string.Join("; ", pendientes) + ".";
        }
    }
}
