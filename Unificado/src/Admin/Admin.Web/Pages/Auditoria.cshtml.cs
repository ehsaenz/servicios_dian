using System.Collections.Generic;
using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DIAN.Admin.Web.Pages
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.Operador)]
    public class AuditoriaModel : PageModel
    {
        private readonly Auditoria _auditoria;

        public AuditoriaModel(Auditoria auditoria)
        {
            _auditoria = auditoria;
        }

        public IReadOnlyList<EntradaAuditoria> Entradas { get; private set; } = new List<EntradaAuditoria>();

        public void OnGet()
        {
            Entradas = _auditoria.Ultimas(200);
        }
    }
}
