-- Esquema de la API multiempresa DIAN. El destino es un MySQL moderno (8.x): la API de facturación electrónica no
-- necesita compatibilidad con el 5.1.73 del ERP. Aun así el SQL es sencillo (tablas InnoDB, sin tipos ni funciones
-- nuevas, sin CHARSET ni COLLATE: los toma la base, que conviene crear en utf8mb4) y no usa claves foráneas.
-- Convención de nombres del proyecto: la llave de dian_clientes es IdCliente y su referencia es ClienteId.
-- Es idempotente: se puede ejecutar más de una vez (`Admin.Web crear-esquema` lo hace con la cadena de conexión).

CREATE TABLE IF NOT EXISTS dian_clientes (
  IdCliente VARCHAR(40) NOT NULL,
  Version INT NOT NULL,
  Activo TINYINT NOT NULL,
  PermiteProduccion TINYINT NOT NULL,
  Nit VARCHAR(20) NOT NULL,
  DV VARCHAR(2) NOT NULL,
  RazonSocial VARCHAR(200) NOT NULL,
  MatriculaMercantil VARCHAR(30) NOT NULL,
  Email VARCHAR(150) NOT NULL,
  NumeroTel VARCHAR(30) NOT NULL,
  PerfilCorreo VARCHAR(50) NULL,
  ArchivoCertificado VARCHAR(100) NULL,
  ClaveCertificadoProtegida TEXT NULL,
  CertificadoSujeto VARCHAR(300) NULL,
  CertificadoVence DATETIME NULL,
  ArchivoLogo VARCHAR(100) NULL,
  PRIMARY KEY (IdCliente)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS dian_claves (
  ClienteId VARCHAR(40) NOT NULL,
  IdClave VARCHAR(16) NOT NULL,
  HashClave CHAR(64) NOT NULL,
  Sufijo VARCHAR(8) NOT NULL,
  Descripcion VARCHAR(100) NOT NULL,
  Activa TINYINT NOT NULL,
  Creada DATETIME NOT NULL,
  PRIMARY KEY (ClienteId, IdClave),
  UNIQUE KEY uq_dian_claves_hash (HashClave)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS dian_software (
  ClienteId VARCHAR(40) NOT NULL,
  TipoSoftware INT NOT NULL,
  Identificador VARCHAR(40) NOT NULL,
  PinProtegido TEXT NOT NULL,
  AmbientePruebas TINYINT NOT NULL,
  TestSetId VARCHAR(40) NOT NULL,
  PRIMARY KEY (ClienteId, TipoSoftware)
) ENGINE=InnoDB;

-- Perfil de impresión de cada sucursal: la matrícula mercantil (llave dentro del cliente) viaja en el XML del documento.
CREATE TABLE IF NOT EXISTS dian_sucursales (
  ClienteId VARCHAR(40) NOT NULL,
  Matricula VARCHAR(30) NOT NULL,
  Nombre VARCHAR(200) NOT NULL,
  RegimenFiscal VARCHAR(100) NOT NULL,
  ArchivoLogo VARCHAR(100) NULL,
  PRIMARY KEY (ClienteId, Matricula)
) ENGINE=InnoDB;

-- Webhook opcional del cliente: una URL fija y su secreto (protegido) para firmar los avisos.
CREATE TABLE IF NOT EXISTS dian_webhooks (
  ClienteId VARCHAR(40) NOT NULL,
  Url VARCHAR(300) NOT NULL,
  SecretoProtegido TEXT NOT NULL,
  PRIMARY KEY (ClienteId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS dian_archivos (
  ClienteId VARCHAR(40) NOT NULL,
  Nombre VARCHAR(100) NOT NULL,
  Version INT NOT NULL,
  Contenido LONGBLOB NOT NULL,
  PRIMARY KEY (ClienteId, Nombre)
) ENGINE=InnoDB;
