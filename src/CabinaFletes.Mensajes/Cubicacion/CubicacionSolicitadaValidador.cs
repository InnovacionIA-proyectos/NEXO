namespace CabinaFletes.Mensajes.Cubicacion;

/// <summary>Invariantes de <see cref="CubicacionSolicitada"/> — ver PublicadorValidado
/// (falla cerrado sin este validador registrado).
///
/// S3-HU-02 (issue #103): extendido de 1 a 9 "Tipo" posibles ("optimizar" + los 8
/// comandos de escritura del plan de cargue). Cada Tipo exige un <c>Solicitud</c> del
/// shape concreto correspondiente (ver MensajesDeCubicacion.cs) — el patrón de tipo en
/// tiempo de ejecución funciona porque este validador corre ANTES de publicar, sobre la
/// misma instancia en memoria con la que <see cref="Infrastructure.Cubicacion.CubicacionPublicadorAdapter"/>
/// construyó el mensaje (no hay que deserializar nada).
///
/// [S3-HU-04.3, issue #78] Ahora son 10: se suma "crear_desde_linea".</summary>
public sealed class CubicacionSolicitadaValidador : IValidadorDeMensaje<CubicacionSolicitada>
{
    private static readonly HashSet<string> TiposConocidos = new(StringComparer.Ordinal)
    {
        "optimizar", "crear_plan", "crear_plan_demo", "guardar_vehiculo", "guardar_item",
        "importar_items", "eliminar_item", "eliminar_plan", "ajustar_colocacion",
        // Cierra el par de "guardar_vehiculo": sin el, esa escritura seguiria saliendo
        // del navegador y S3-HU-05 no puede cerrar. Ver EliminarVehiculoInput.
        "eliminar_vehiculo",
        // [S3-HU-04.3, issue #78] El worker de Cubicador ya implementa este tipo desde
        // antes de esta HU (cubicador/worker/main.py) — ver CrearPlanDesdeLineaCommand.
        "crear_desde_linea",
    };

    private static readonly HashSet<string> PuertaPosicionesValidas = new(StringComparer.Ordinal)
    {
        "trasera", "frontal", "costado_izquierdo", "costado_derecho",
    };

    public IReadOnlyList<string> Validar(CubicacionSolicitada mensaje)
    {
        var motivos = new List<string>();

        if (string.IsNullOrWhiteSpace(mensaje.PlanId) || !Guid.TryParse(mensaje.PlanId, out _))
            motivos.Add("PlanId debe ser un GUID válido: es la identidad con la que Cubicador crea o busca la fila.");

        if (string.IsNullOrWhiteSpace(mensaje.CorrelationId))
            motivos.Add("CorrelationId no puede ser vacío: es con lo que se correlaciona la respuesta.");

        if (string.IsNullOrWhiteSpace(mensaje.Tipo))
        {
            motivos.Add("Tipo no puede ser vacío: el worker de Cubicador lo usa para elegir el caso de uso.");
            return motivos;
        }

        if (!TiposConocidos.Contains(mensaje.Tipo))
        {
            motivos.Add($"Tipo '{mensaje.Tipo}' desconocido. Tipos válidos: {string.Join(", ", TiposConocidos)}.");
            return motivos;
        }

        if (mensaje.Solicitud is null)
        {
            motivos.Add($"Solicitud no puede ser nula para Tipo '{mensaje.Tipo}'.");
            return motivos;
        }

        switch (mensaje.Tipo)
        {
            case "optimizar":
                ValidarOptimizar(mensaje.Solicitud, motivos);
                break;
            case "crear_plan":
                ValidarCrearPlan(mensaje.Solicitud, motivos);
                break;
            case "crear_plan_demo":
                ExigirTipo<CrearPlanDemoInput>(mensaje.Solicitud, "crear_plan_demo", motivos);
                break;
            case "crear_desde_linea":
                ValidarCrearDesdeLinea(mensaje.Solicitud, motivos);
                break;
            case "guardar_vehiculo":
                ValidarGuardarVehiculo(mensaje.Solicitud, motivos);
                break;
            case "guardar_item":
                ValidarGuardarItem(mensaje.Solicitud, "Solicitud", motivos);
                break;
            case "importar_items":
                ValidarImportarItems(mensaje.Solicitud, motivos);
                break;
            case "eliminar_item":
                ValidarEliminarItem(mensaje.Solicitud, motivos);
                break;
            case "eliminar_plan":
                ExigirTipo<EliminarPlanInput>(mensaje.Solicitud, "eliminar_plan", motivos);
                break;
            case "eliminar_vehiculo":
                ExigirTipo<EliminarVehiculoInput>(mensaje.Solicitud, "eliminar_vehiculo", motivos);
                break;
            case "ajustar_colocacion":
                ValidarAjustarColocacion(mensaje.Solicitud, motivos);
                break;
        }

        return motivos;
    }

    private static void ExigirTipo<T>(object solicitud, string tipo, List<string> motivos)
    {
        if (solicitud is not T)
            motivos.Add($"Solicitud debe ser de tipo {typeof(T).Name} para Tipo '{tipo}'.");
    }

    private static void ValidarOptimizar(object solicitud, List<string> motivos)
    {
        if (solicitud is not SolicitudDeCubicacion s)
        {
            motivos.Add("Solicitud debe ser de tipo SolicitudDeCubicacion para Tipo 'optimizar'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(s.Estrategia))
            motivos.Add("Solicitud.Estrategia no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(s.Algoritmo))
            motivos.Add("Solicitud.Algoritmo no puede ser vacío.");
    }

    private static void ValidarCrearPlan(object solicitud, List<string> motivos)
    {
        if (solicitud is not CrearPlanInput s)
        {
            motivos.Add("Solicitud debe ser de tipo CrearPlanInput para Tipo 'crear_plan'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(s.Nombre))
            motivos.Add("Solicitud.Nombre no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(s.VehiculoId) || !Guid.TryParse(s.VehiculoId, out _))
            motivos.Add("Solicitud.VehiculoId debe ser un GUID válido.");
    }

    /// <summary>[S3-HU-04.3, issue #78] "crear_desde_linea".</summary>
    private static void ValidarCrearDesdeLinea(object solicitud, List<string> motivos)
    {
        if (solicitud is not CrearPlanDesdeLineaInput s)
        {
            motivos.Add("Solicitud debe ser de tipo CrearPlanDesdeLineaInput para Tipo 'crear_desde_linea'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(s.LineaSolicitudId) || !Guid.TryParse(s.LineaSolicitudId, out _))
            motivos.Add("Solicitud.LineaSolicitudId debe ser un GUID válido.");
    }

    private static void ValidarGuardarVehiculo(object solicitud, List<string> motivos)
    {
        if (solicitud is not GuardarVehiculoInput s)
        {
            motivos.Add("Solicitud debe ser de tipo GuardarVehiculoInput para Tipo 'guardar_vehiculo'.");
            return;
        }

        if (s.Id is not null && !Guid.TryParse(s.Id, out _))
            motivos.Add("Solicitud.Id debe ser un GUID válido cuando se manda (actualización).");

        if (string.IsNullOrWhiteSpace(s.Nombre))
            motivos.Add("Solicitud.Nombre no puede ser vacío.");

        if (s.LargoInternoMm <= 0 || s.AnchoInternoMm <= 0 || s.AltoInternoMm <= 0)
            motivos.Add("Solicitud: las dimensiones internas deben ser positivas.");

        if (s.CapacidadKg <= 0)
            motivos.Add("Solicitud.CapacidadKg debe ser positiva.");

        if (s.PuertaAnchoMm <= 0 || s.PuertaAltoMm <= 0)
            motivos.Add("Solicitud: las dimensiones de la puerta deben ser positivas.");

        if (!PuertaPosicionesValidas.Contains(s.PuertaPosicion))
            motivos.Add($"Solicitud.PuertaPosicion inválida: '{s.PuertaPosicion}'.");

        if (s.Ejes is null)
            motivos.Add("Solicitud.Ejes no puede ser nulo (puede ser una lista vacía).");
    }

    private static void ValidarGuardarItem(object solicitud, string prefijo, List<string> motivos)
    {
        if (solicitud is not GuardarItemInput s)
        {
            motivos.Add($"{prefijo} debe ser de tipo GuardarItemInput.");
            return;
        }

        if (s.Id is not null && !Guid.TryParse(s.Id, out _))
            motivos.Add($"{prefijo}.Id debe ser un GUID válido cuando se manda (actualización).");

        if (string.IsNullOrWhiteSpace(s.Descripcion))
            motivos.Add($"{prefijo}.Descripcion no puede ser vacía.");

        if (s.LargoMm <= 0 || s.AnchoMm <= 0 || s.AltoMm <= 0)
            motivos.Add($"{prefijo}: las dimensiones deben ser positivas.");

        if (s.PesoKg <= 0)
            motivos.Add($"{prefijo}.PesoKg debe ser positivo.");

        if (s.Cantidad is < 1 or > 2000)
            motivos.Add($"{prefijo}.Cantidad debe estar entre 1 y 2.000.");
    }

    private static void ValidarImportarItems(object solicitud, List<string> motivos)
    {
        if (solicitud is not ImportarItemsInput s)
        {
            motivos.Add("Solicitud debe ser de tipo ImportarItemsInput para Tipo 'importar_items'.");
            return;
        }

        if (s.Items is null || s.Items.Count is < 1 or > 2000)
        {
            motivos.Add("Solicitud.Items debe traer entre 1 y 2.000 unidades.");
            return;
        }

        for (var i = 0; i < s.Items.Count; i++)
            ValidarGuardarItem(s.Items[i], $"Solicitud.Items[{i}]", motivos);
    }

    private static void ValidarEliminarItem(object solicitud, List<string> motivos)
    {
        if (solicitud is not EliminarItemInput s)
        {
            motivos.Add("Solicitud debe ser de tipo EliminarItemInput para Tipo 'eliminar_item'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(s.ItemId) || !Guid.TryParse(s.ItemId, out _))
            motivos.Add("Solicitud.ItemId debe ser un GUID válido.");
    }

    private static void ValidarAjustarColocacion(object solicitud, List<string> motivos)
    {
        if (solicitud is not AjustarColocacionInput s)
        {
            motivos.Add("Solicitud debe ser de tipo AjustarColocacionInput para Tipo 'ajustar_colocacion'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(s.ColocacionId) || !Guid.TryParse(s.ColocacionId, out _))
            motivos.Add("Solicitud.ColocacionId debe ser un GUID válido.");

        var traeAlgunAjuste = s.X is not null || s.Y is not null || s.Z is not null
            || s.OrientacionIndice is not null || s.Bloqueada is not null
            || s.Retirar is true || s.Reinsertar is true;
        if (!traeAlgunAjuste)
            motivos.Add("Solicitud no trae ningún ajuste (x/y/z/orientacionIndice/bloqueada/retirar/reinsertar).");
    }
}
