namespace Nexo.Domain.Enums;

/// <summary>
/// Roles de la aplicación. Los dos roles de negocio son Comercial (captura de
/// solicitudes y servicios conexos en la mesa) y AdminTarifas (todos los módulos
/// excepto captura; costea sin gestionar conexos). Admin es el superusuario técnico.
/// </summary>
public enum RolUsuario { CoordinadorFletes, LiderProceso, ServiciosLogisticos, Admin, Comercial, AdminTarifas }

public enum TipoSolicitud { Spot, Licitacion }

public enum ModalidadSolicitud { Importacion, Exportacion, Nacional, Extradimensionada, CargaGeneral }

public enum EstadoSolicitud { Pendiente, EnCosteo, Entregada, Descartada }

public enum TipologiaCarga { Contenedor, CargaSuelta, GranelSolido, GranelLiquido, Alimentos, Refrigerados, CargaSeca }

/// <summary>
/// Línea de negocio de SAP TM (catálogo dbsolucionesTI.tblSAPLineaNegocio). Alinea la
/// captura con el formulario "Consultar Tarifa" para poder cruzar contra las tarifas SAP.
/// </summary>
public enum LineaNegocio { PuertoAInterior, InteriorAPuerto, Nacional, DevolucionContenedor, Andino, Licitaciones }

/// <summary>Clase de viaje de SAP TM (catálogo dbsolucionesTI.tblSAPClaseViaje).</summary>
public enum ClaseViaje { Contenedor, Cupo, CargaSuelta, Extradimensionado }

public enum EstadoLinea { PorCostear, AutoResuelta, Revisada }

/// <summary>
/// Por qué una línea quedó sin costear. Los dos tipos tienen ciclos de vida OPUESTOS y
/// por eso se distinguen:
///
///   Tecnica — una fuente falló. Se reintenta y se resuelve sola. Tiene reloj.
///   Manual  — no hay tarifa válida. Espera al Administrador de Tarifas. NO se
///             reintenta y NO tiene reloj: esa persona puede tener otros pliegos
///             delante, y ponerle vencimiento marcaría como fallido algo que estaba
///             esperando bien.
/// </summary>
public enum TipoPendienteDeCosteo { Tecnica, Manual }

public enum CondicionCarga { Cargado, Vacio }

public enum FuenteFlete { Sicetac, Historica, Manual, Tarifario }

public enum EstadoCotizacion { Borrador, Entregada }

// SinDefinir primero (valor CLR por defecto) para alinear con el default de BD.
public enum ResultadoCotizacion { SinDefinir, Ganada, Perdida }
