# -*- coding: utf-8 -*-
import csv, uuid

def nid():
    return str(uuid.uuid4())

OUT = "."

# ---------- Tabla 1: vehiculos ----------
vehiculos = [
    # codigo, nombre, largo, ancho, alto, capacidad_kg
    ("2L2", "Turbo 2L2", 4.5, 2.2, 2.3, 4000),
    ("2", "Sencillo C2", 6.5, 2.4, 2.4, 8500),
    ("3", "Dobletroque C3", 8.0, 2.4, 2.6, 17000),
    ("2S2", "Minimula 2S2", 12.2, 2.4, 2.6, 18000),
    ("3S3", "Tractomula 3S3", 12.2, 2.4, 2.6, 34000),
    ("3S3_FURGON", "Tractomula Furgon 3S3", 12.2, 2.4, 2.6, 30000),
]
vehiculo_ids = {}
with open(f"{OUT}/vehiculos.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "Codigo", "Nombre", "LargoPlancha", "AnchoPlancha", "AltoCarroceria", "CapacidadMaxima", "Activo", "CreadoPor"])
    for codigo, nombre, l, a, h, cap in vehiculos:
        vid = nid()
        vehiculo_ids[codigo] = vid
        w.writerow([vid, codigo, nombre, l, a, h, cap, "true", "seeder"])

# ---------- Tabla 2: TiposCarroceria ----------
# Catalogo SIN fusionar sinonimos: Plataforma y Planchon quedan separados
# (la matriz los usa como alternativas entre si, ej. filas "Vigas" y "Bobinas de
# acero"), y Furgon refrigerado / climatizado / ventilado tambien quedan
# separados (son necesidades distintas, no el mismo codigo).
carrocerias = ["FURGON", "FURGON_REFRIGERADO", "FURGON_CLIMATIZADO", "FURGON_VENTILADO",
               "ESTACAS", "PLATAFORMA", "PLANCHON", "PORTAVIDRIOS",
               "CONTENEDOR_SECO", "CONTENEDOR_REFRIGERADO"]
carroceria_ids = {}
with open(f"{OUT}/tiposCarroceria.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "TipoCarroceria", "Activo", "CreadoPor"])
    for c in carrocerias:
        cid = nid()
        carroceria_ids[c] = cid
        w.writerow([cid, c, "true", "seeder"])

# ---------- Tabla 4: relacionVehiculosCarroceria ----------
# La hoja "Dimensiones y Peso" solo habla de Estacas/Furgon/Plancha en
# terminos genericos (no distingue refrigerado/climatizado/ventilado ni
# Planchon de Plataforma) -- "Plancha" ahi se interpreta como PLATAFORMA.
rel = {
    "2L2": ["ESTACAS", "FURGON", "PLATAFORMA"],
    "2": ["ESTACAS", "FURGON", "PLATAFORMA"],
    "3": ["ESTACAS", "PLATAFORMA"],
    "2S2": ["ESTACAS", "FURGON", "PLATAFORMA"],
    "3S3": ["ESTACAS", "PLATAFORMA"],
    "3S3_FURGON": ["FURGON"],
}
with open(f"{OUT}/relacionVehiculosCarroceria.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "IdTipoVehiculo", "CodigoVehiculo", "IdCarroceria", "CodigoCarroceria", "CreadoPor"])
    for vcod, cs in rel.items():
        for c in cs:
            w.writerow([nid(), vehiculo_ids[vcod], vcod, carroceria_ids[c], c, "seeder"])

# ---------- Tabla 5: categoriaMercancia ----------
# Solo categorias que agrupan mercancias REALES de la matriz (nada heredado
# del _INCOMPATIBILIDADES del codigo viejo -- ahi no hay nada oxidante ni
# corrosivo en estas 31 filas).
categorias = ["GENERAL", "ALIMENTOS", "FARMACEUTICO", "PERECEDERO_NO_ALIMENTO", "FERTILIZANTE",
              "COMBUSTIBLE", "METALICO_CONSTRUCCION", "VIDRIO", "GAS_INDUSTRIAL"]
categoria_ids = {}
with open(f"{OUT}/categoriaMercancia.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "NombreCategoria", "CreadoPor"])
    for c in categorias:
        cid = nid()
        categoria_ids[c] = cid
        w.writerow([cid, c, "seeder"])

# ---------- Tabla 6: reglasIncompatibilidad ----------
# La matriz de 31 mercancias NO define ninguna regla de incompatibilidad --
# eso vive en otra hoja/criterio aparte, que todavia no existe. Se deja
# vacia (solo encabezado) a proposito: no se inventan reglas sin respaldo
# en el Excel. Hay que construirla con operaciones (ver brecha #4 de
# propuesta-seleccion-vehiculos.md, seccion 3.3).
with open(f"{OUT}/reglasIncompatibilidad.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "IdCategoria1", "Categoria1", "IdCategoria2", "Categoria2", "Motivo", "CreadoPor"])

# ---------- Tabla 3: tiposMercancia ----------
# cond: 'N' = Si (no condicionado), 'Y' = requiere validacion obligatoria AdminTarifas
filas = [
    ("Cajas de carton / mercancia empacada", "GENERAL", "FURGON", "ESTACAS", "N",
     "Validar dimensiones, apilabilidad y resistencia del empaque."),
    ("Alimentos secos empacados", "ALIMENTOS", "FURGON", "ESTACAS", "N",
     "Producto empacado; verificar requisitos sanitarios."),
    ("Electrodomesticos embalados", "GENERAL", "FURGON", "ESTACAS", "N",
     "Proteger de lluvia, golpes y vibracion."),
    ("Muebles y enseres embalados", "GENERAL", "FURGON", "ESTACAS", "N",
     "Validar piezas voluminosas y posibilidad de apilamiento."),
    ("Productos farmaceuticos a temperatura ambiente", "FARMACEUTICO", "FURGON", "FURGON_CLIMATIZADO", "N",
     "Respetar condiciones declaradas por fabricante."),
    ("Productos farmaceuticos refrigerados", "FARMACEUTICO", "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", "N",
     "Definir rango de temperatura y monitoreo requerido."),
    ("Alimentos congelados empacados", "ALIMENTOS", "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", "N",
     "Validar temperatura, ventilacion y estiba."),
    ("Carnes y productos carnicos empacados", "ALIMENTOS", "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", "N",
     "Solo carga debidamente empacada; cumplir cadena de frio."),
    ("Frutas y verduras empacadas", "ALIMENTOS", "FURGON_VENTILADO", "CONTENEDOR_REFRIGERADO", "N",
     "La temperatura y ventilacion dependen del producto (principal puede ser ventilado o refrigerado)."),
    ("Flores y perecederos empacados", "PERECEDERO_NO_ALIMENTO", "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", "N",
     "Definir temperatura y ventilacion segun especificacion (principal puede ser refrigerado o climatizado)."),
    ("Productos lacteos empacados", "ALIMENTOS", "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", "N",
     "Mantener cadena de frio."),
    ("Bebidas embotelladas o enlatadas", "ALIMENTOS", "FURGON", "ESTACAS", "N",
     "Considerar peso por pallet y resistencia del embalaje."),
    ("Fertilizantes ensacados", "FERTILIZANTE", "FURGON", "ESTACAS", "N",
     "Evitar humedad y contaminacion."),
    ("Pallets de mercancia general", "GENERAL", "FURGON", "ESTACAS", "N",
     "Cubicacion por pallet o unidad logistica; revisar espacios no aprovechables."),
    ("Estibas de madera (mercancia)", "COMBUSTIBLE", "PLATAFORMA", "ESTACAS", "N",
     "Cubicacion por dimensiones exteriores y forma de estiba."),
    ("Materiales de construccion empacados", "METALICO_CONSTRUCCION", "ESTACAS", "FURGON", "N",
     "Ej.: cajas o paquetes; excluir material suelto a granel."),
    ("Ladrillos y bloques paletizados", "METALICO_CONSTRUCCION", "ESTACAS", "PLATAFORMA", "N",
     "Validar peso por pallet y resistencia."),
    ("Varillas y perfiles metalicos en paquetes", "METALICO_CONSTRUCCION", "PLATAFORMA", "ESTACAS", "N",
     "Cargas largas; validar longitud, amarres y salientes."),
    ("Tuberias en paquetes", "METALICO_CONSTRUCCION", "PLATAFORMA", "ESTACAS", "N",
     "Validar longitud, diametro y soportes."),
    ("Vigas y estructuras metalicas en piezas", "METALICO_CONSTRUCCION", "PLATAFORMA", "PLANCHON", "Y",
     "Si excede dimensiones ordinarias, tratar como carga especial."),
    ("Laminas de acero en paquetes", "METALICO_CONSTRUCCION", "PLATAFORMA", "ESTACAS", "N",
     "Verificar apoyos, proteccion de bordes y peso (principal puede ser plataforma o planchon)."),
    ("Bobinas de acero", "METALICO_CONSTRUCCION", "PLATAFORMA", "PLANCHON", "Y",
     "Cubicacion requiere orientacion, diametro, ancho y cunas portabobinas; validar estabilidad."),
    ("Madera aserrada en paquetes", "COMBUSTIBLE", "PLATAFORMA", "ESTACAS", "N",
     "Validar longitud, paquete y sujecion."),
    ("Postes de concreto en unidades", "METALICO_CONSTRUCCION", "PLATAFORMA", "ESTACAS", "Y",
     "Carga larga; validar apoyos y puntos de amarre."),
    ("Papel y carton en cajas o pacas", "COMBUSTIBLE", "FURGON", "ESTACAS", "N",
     "Proteger de humedad."),
    ("Papel en rollos", "COMBUSTIBLE", "FURGON", "PLATAFORMA", "Y",
     "Definir diametro, ancho, orientacion y metodo de sujecion (principal puede ser furgon o plataforma con cunas)."),
    ("Vidrio plano / laminas", "VIDRIO", "PORTAVIDRIOS", "PLATAFORMA", "Y",
     "Requiere bastidor, orientacion y proteccion especifica."),
    ("Ventanas y fachadas embaladas", "GENERAL", "FURGON", "PLATAFORMA", "Y",
     "Cubicacion por embalaje; controlar fragilidad y orientacion."),
    ("Cilindros de gases industriales", "GAS_INDUSTRIAL", "PLATAFORMA", "FURGON_VENTILADO", "Y",
     "No cubicar como cajas comunes; validar clasificacion, segregacion y sujecion."),
    ("Mercancia general de exportacion paletizada", "GENERAL", "FURGON", "ESTACAS", "N",
     "Cubicacion por pallet/embalaje y patron de estiba (principal puede ser furgon o contenedor seco)."),
    ("Carga contenerizada en cajas o pallets", "GENERAL", "CONTENEDOR_SECO", "FURGON", "N",
     "El contenedor es unidad de transporte; cubicacion interna depende de medidas utiles y limites de carga."),
]

with open(f"{OUT}/tiposMercancia.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "NombreMercancia", "IdCategoria", "Categoria_ref", "Cubicable", "CondicionadaCubicacion",
                "ComentarioCondicion", "IdCarroceriaPrincipal", "CarroceriaPrincipal_ref",
                "IdCarroceriaSecundaria", "CarroceriaSecundaria_ref", "CarroceriaSecundariaCondicionada",
                "Activo", "CreadoPor"])
    for nombre, cat, princ, sec, cond, com in filas:
        sec_id = carroceria_ids[sec] if sec else ""
        sec_cond = "true" if sec else "false"
        w.writerow([nid(), nombre, categoria_ids[cat], cat, "true", "true" if cond == "Y" else "false",
                    com, carroceria_ids[princ], princ, sec_id, sec, sec_cond, "true", "seeder"])

# ---------- Tabla 9: parametrosMotor ----------
params = [
    ("k_max", "4", "Tope de vehiculos por grupo antes de sin seleccion automatica"),
    ("factor_vol_util", "0.90", "Fraccion del volumen util aprovechable en la practica"),
    ("usar_alternativa", "solo_respaldo", "Si la carroceria alternativa entra siempre o solo como respaldo"),
    ("umbral_alerta_llenado", "0.75", "Por debajo de este porcentaje de llenado se marca alerta"),
    ("margen_cg", "0.30", "Centro de gravedad dentro de +-30% del centro geometrico"),
]
with open(f"{OUT}/parametrosMotor.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "Clave", "Valor", "Descripcion", "CreadoPor"])
    for clave, valor, desc in params:
        w.writerow([nid(), clave, valor, desc, "seeder"])

print("listo")
