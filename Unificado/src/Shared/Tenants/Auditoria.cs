using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DIAN.Tenants
{
    public sealed class EntradaAuditoria
    {
        public DateTime FechaUtc { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
    }

    /// <summary>Registro de quién cambió qué. Nunca recibe valores secretos (claves, PIN).</summary>
    public sealed class Auditoria
    {
        private static readonly object Candado = new object();
        private readonly string _archivo;

        public Auditoria(string raiz)
        {
            Directory.CreateDirectory(raiz);
            _archivo = Path.Combine(raiz, "auditoria.jsonl");
        }

        public void Registrar(string usuario, string accion, string cliente, string detalle = "")
        {
            var linea = JsonSerializer.Serialize(new EntradaAuditoria
            {
                FechaUtc = DateTime.UtcNow,
                Usuario = usuario,
                Accion = accion,
                Cliente = cliente,
                Detalle = detalle
            });
            lock (Candado)
            {
                File.AppendAllText(_archivo, linea + Environment.NewLine);
            }
        }

        /// <summary>Las últimas entradas, de la más reciente a la más antigua.</summary>
        public IReadOnlyList<EntradaAuditoria> Ultimas(int cantidad = 200)
        {
            if (!File.Exists(_archivo))
            {
                return new List<EntradaAuditoria>();
            }

            string[] lineas;
            lock (Candado)
            {
                lineas = File.ReadAllLines(_archivo);
            }

            return lineas.Reverse().Take(cantidad)
                .Select(l => JsonSerializer.Deserialize<EntradaAuditoria>(l)!)
                .ToList();
        }
    }
}
