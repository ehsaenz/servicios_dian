# Simulador del ERP

Herramienta de consola que se comporta como un ERP al llamar a la API de DIAN (`Pos.Api`). Sirve para probar la API sin el ERP real.

- Envía JSON con la cabecera `x-api-key`, como lo hace un ERP. La clave se lee solo de `DIAN_API_KEY`; nunca se imprime.
- Sin `--confirmar` no envía nada: muestra la petición y avisa. Con `--set` consume consecutivos del set de pruebas.
- No genera documentos. El JSON del documento lo arma el ERP o un archivo de prueba que usted entrega.

Ejemplos (PowerShell):

    $env:DIAN_API_KEY = "<clave del cliente de habilitación>"
    dotnet run -- salud
    dotnet run -- quien
    dotnet run -- enviar --tipo factura --archivo .\factura.json --set              (muestra, no envía)
    dotnet run -- enviar --tipo factura --archivo .\factura.json --set --confirmar
    dotnet run -- estado --numero SETP990000001

Pendiente: conectar a la base de datos de QuantyERP para tomar datos reales de productos y facturas. No está hecho: requiere credenciales de solo lectura, y los datos de clientes deben anonimizarse antes de usarse como ejemplo.
