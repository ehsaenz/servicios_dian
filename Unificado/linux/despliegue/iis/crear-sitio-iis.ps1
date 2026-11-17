$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
$nombre = 'DIAN.Linux'; $puerto = 47125; $ruta = 'C:\inetpub\dian-linux'
$ref = Get-Website -Name 'QuantyERP.Linux.API'
$pool = $ref.applicationPool
$hash = (Get-WebBinding -Name 'QuantyERP.Linux.API' -Protocol https | Select-Object -First 1).certificateHash
$store = (Get-WebBinding -Name 'QuantyERP.Linux.API' -Protocol https | Select-Object -First 1).certificateStoreName
"pool de referencia: $pool ; certificado: " + $hash.Substring(0,8) + "... en $store"
if (-not $hash) { throw 'No encuentro el certificado del sitio de referencia.' }
if (Get-Website -Name $nombre -ErrorAction SilentlyContinue) { "El sitio $nombre ya existe." } else {
  New-Item -ItemType Directory -Path $ruta -Force | Out-Null
  @'
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="ProxyDianVm" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://172.31.50.10:8090/{R:1}" />
        </rule>
      </rules>
    </rewrite>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="5242880" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
'@ | Set-Content -Path "$ruta\web.config" -Encoding UTF8
  New-Website -Name $nombre -PhysicalPath $ruta -ApplicationPool $pool -Port $puerto -Ssl | Out-Null
  $b = Get-WebBinding -Name $nombre -Protocol https
  $b.AddSslCertificate($hash, $store)
  "Sitio $nombre creado en el puerto $puerto."
}
if (-not (Get-NetFirewallRule -DisplayName 'DIAN IIS 47125' -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -DisplayName 'DIAN IIS 47125' -Direction Inbound -Protocol TCP -LocalPort $puerto -Action Allow | Out-Null
  "Regla de Windows Firewall creada para $puerto."
}
(Get-Website -Name $nombre).State
