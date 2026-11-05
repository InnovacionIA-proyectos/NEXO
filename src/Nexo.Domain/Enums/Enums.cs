namespace Nexo.Domain.Enums;

/// <summary>Roles de la aplicación. Comercial captura solicitudes y conexos; AdminTarifas accede a todo excepto captura (costea sin gestionar conexos); Admin es el superusuario técnico.</summary>
public enum RolUsuario { AdminBiblioteca, LiderProceso, ServiciosLogisticos, Admin, Comercial, AdminTarifas }

public enum TipoSolicitud { Spot, Licitacion }

public enum ModalidadSolicitud { Importacion, Exportacion, Nacional, Extradimensionada, CargaGeneral }

public enum EstadoSolicitud { Pendiente, EnCosteo, Entregada, Descartada }

public enum TipologiaCarga { Contenedor, CargaSuelta, GranelSolido, GranelLiquido, Alimentos, Refrigerados, CargaSeca }

/// <summary>Línea de negocio de SAP TM; permite cruzar la captura contra las tarifas SAP.</summary>
public enum LineaNegocio { PuertoAInterior, InteriorAPuerto, Nacional, DevolucionContenedor, Andino, Licitaciones }

/// <summary>Clase de viaje de SAP TM.</summary>
public enum ClaseViaje { Contenedor, Cupo, CargaSuelta, Extradimensionado }

// Rechazada: el Administrador de Tarifas la dejó sin costear a propósito, con motivo, para poder entregar el resto.
public enum EstadoLinea { PorCostear, AutoResuelta, Revisada, Rechazada }

/// <summary>
/// Por qué una línea quedó sin costear. Tecnica: una fuente falló, se reintenta y tiene reloj.
/// Manual: no hay tarifa válida, espera al Administrador de Tarifas; no se reintenta ni vence.
/// </summary>
public enum TipoPendienteDeCosteo { Tecnica, Manual }

public enum CondicionCarga { Cargado, Vacio }

public enum FuenteFlete { Sicetac, Historica, Manual, Tarifario }

public enum EstadoCotizacion { Borrador, Entregada }

// SinDefinir va primero (valor CLR por defecto) para alinear con el default de BD.
public enum ResultadoCotizacion { SinDefinir, Ganada, Perdida }
