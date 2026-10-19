# -*- coding: utf-8 -*-
import csv, uuid

# Namespace fijo propio del proyecto, para generar Id DETERMINISTICOS (no
# aleatorios): el mismo texto de "seed" siempre produce el mismo Id, en
# cualquier maquina, cualquier dia que se corra este script. Esto es lo que
# permite que cargar el seeder dos veces contra la misma BD no duplique
# filas ni pise una correccion hecha a mano por el CRUD -- el script de
# carga (ejecutar_schema_y_seeders.py) simplemente salta cualquier Id que
# ya exista en la tabla destino.
_NAMESPACE_CUBICADOR = uuid.UUID("c9b1b1a0-6f3e-4b1a-9c2d-a1b2c3d40001")

def nid(seed: str) -> str:
    return str(uuid.uuid5(_NAMESPACE_CUBICADOR, seed))

OUT = "."

# ---------- Tabla 1: vehiculos ----------
# "Tractomula Furgon 3S3" ya NO es una fila aparte -- es el mismo 3S3 (el
# codigo debe quedar unico para el matching con SICE-TAC/tarifario). La
# diferencia de capacidad con Furgon (30000 vs 34000 kg) se resuelve en
# relacionVehiculosCarroceria (ver mas abajo), no duplicando el vehiculo.
# Medidas en cm (cruzado con el seeder de Brayan -- coinciden exacto con las
# nuestras convertidas). Codigos C2/C3 tal como estan en el Excel (Brayan
# confirma lo mismo). La altura NO es un default fijo -- es el limite LEGAL
# de altura para ese tipo de vehiculo segun la reglamentacion de transito
# (asi la nombra el seeder de Brayan: "AlturaMaximaLegalCm"), no una medida
# cualquiera del fabricante. Ambos seeders coinciden en que no cambia segun
# carroceria, asi que es propiedad del vehiculo, no de la combinacion.
vehiculos = [
    # codigo, nombre, largo_cm, ancho_cm, altura_maxima_reglamentaria_cm, capacidad_kg
    ("2L2", "Turbo 2L2", 450, 220, 230, 4000),
    ("C2", "Sencillo C2", 650, 240, 240, 8500),
    ("C3", "Dobletroque C3", 800, 240, 260, 17000),
    ("2S2", "Minimula 2S2", 1220, 240, 260, 18000),
    ("3S3", "Tractomula 3S3", 1220, 240, 260, 34000),
]
vehiculo_ids = {}
with open(f"{OUT}/vehiculos.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "CodigoVehiculo", "NombreVehiculo", "LargoCm", "AnchoCm", "AlturaMaximaReglamentariaCm", "CapacidadMaxima", "Activo", "CreadoPor"])
    for codigo, nombre, l, a, h, cap in vehiculos:
        vid = nid(f"vehiculo:{codigo}")
        vehiculo_ids[codigo] = vid
        w.writerow([vid, codigo, nombre, l, a, h, cap, "true", "seeder"])

# ---------- Tabla 2: TiposCarroceria ----------
# Catalogo SIN fusionar sinonimos: Plataforma y Planchon quedan separados
# (la matriz los usa como alternativas entre si, ej. filas "Vigas" y "Bobinas de
# acero"), y Furgon refrigerado / climatizado / ventilado tambien quedan
# separados (son necesidades distintas, no el mismo codigo). Estructura
# Codigo+Nombre cruzada con el seeder de Brayan, pero el catalogo sigue
# siendo el nuestro (mas granular que el de el).
carrocerias = [
    ("FURGON", "Furgon"),
    ("FURGON_REFRIGERADO", "Furgon refrigerado"),
    ("FURGON_CLIMATIZADO", "Furgon climatizado"),
    ("FURGON_VENTILADO", "Furgon ventilado"),
    ("ESTACAS", "Estacas"),
    ("PLATAFORMA", "Plataforma"),
    ("PLANCHON", "Planchon"),
    ("PORTAVIDRIOS", "Estructura portavidrios"),
    ("CONTENEDOR_SECO", "Contenedor seco"),
    ("CONTENEDOR_REFRIGERADO", "Contenedor refrigerado"),
    # Rescatadas del catalogo viejo de cubicacion rapida (9 arquetipos
    # hardcodeados): CAMA_BAJA y PORTACONTENEDORES no estaban en los
    # Excel nuevos, pero si existian antes y se decidio mantenerlas.
    ("CAMA_BAJA", "Cama baja"),
    ("PORTACONTENEDORES", "Portacontenedores"),
    # Cruzadas contra el catalogo real de SAP (tblSAPTiposCarroseria) --
    # existen en SAP pero no teniamos ninguna fila para ellas. Igual que
    # CAMA_BAJA/PORTACONTENEDORES: se agregan al catalogo SIN relacion en
    # relacionVehiculosCarroceria (con que vehiculo(s) empareja cada una lo
    # decide el Administrador de Fletes desde el CRUD, no se infiere aqui).
    ("TANQUE", "Tanque"),
    ("TOLVA", "Tolva"),
    ("VOLCO", "Volco"),
    ("GRUA", "Grua"),
    ("NINERA", "Niñera"),
]
carroceria_ids = {}
with open(f"{OUT}/tiposCarroceria.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "CodigoCarroceria", "NombreCarroceria", "Activo", "CreadoPor"])
    for c, nombre in carrocerias:
        cid = nid(f"carroceria:{c}")
        carroceria_ids[c] = cid
        w.writerow([cid, c, nombre, "true", "seeder"])

# ---------- Tabla 4: relacionVehiculosCarroceria ----------
# La hoja "Dimensiones y Peso" solo habla de Estacas/Furgon/Plancha en
# terminos genericos (no distingue refrigerado/climatizado/ventilado ni
# Planchon de Plataforma) -- "Plancha" ahi se interpreta como PLATAFORMA.
# Cada carrocería va con (usa_capacidad_vehiculo, capacidad_especifica).
# Por defecto usa la capacidad del vehiculo (true, 0 -- no se lee).
# 3S3 + FURGON es la unica excepcion real detectada en el Excel: con Furgon
# carga 30000 kg en vez de los 34000 normales del 3S3 con Estacas/Plataforma.
rel = {
    "2L2": [("ESTACAS", True, 0), ("FURGON", True, 0), ("PLATAFORMA", True, 0)],
    "C2": [("ESTACAS", True, 0), ("FURGON", True, 0), ("PLATAFORMA", True, 0)],
    "C3": [("ESTACAS", True, 0), ("PLATAFORMA", True, 0)],
    "2S2": [("ESTACAS", True, 0), ("FURGON", True, 0), ("PLATAFORMA", True, 0)],
    "3S3": [("ESTACAS", True, 0), ("PLATAFORMA", True, 0), ("FURGON", False, 30000)],
}
# Las variantes de Furgon (refrigerado/climatizado/ventilado) aplican a los
# mismos vehiculos que llevan FURGON liso, con la misma regla de capacidad
# que ya tenia ese vehiculo con Furgon (mismo chasis, solo cambia el
# acondicionamiento de la caja) -- C3 no tiene ninguna porque no lleva
# Furgon en el Excel.
for vcod, cs in list(rel.items()):
    for c, usa_cap, cap_especifica in cs:
        if c == "FURGON":
            for variante in ("FURGON_REFRIGERADO", "FURGON_CLIMATIZADO", "FURGON_VENTILADO"):
                rel[vcod].append((variante, usa_cap, cap_especifica))
            break
# CAMA_BAJA y PORTACONTENEDORES (arriba, Tabla 2) NO llevan fila en esta
# relacion -- a proposito. Solo se emparejan aqui las carrocerias que salen
# del Excel "Dimensiones y Peso por vehiculo". Esas 2 son un rescate del
# catalogo viejo que todavia no tiene emparejamiento real confirmado con
# ningun vehiculo de este Excel -- queda pendiente de decidir, no se infiere.
with open(f"{OUT}/relacionVehiculosCarroceria.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "IdTipoVehiculo", "IdCarroceria",
                "UsaCapacidadMaximaDelVehiculo", "CapacidadMaximaEspecifica", "CreadoPor"])
    for vcod, cs in rel.items():
        for c, usa_cap, cap_especifica in cs:
            fila_id = nid(f"relacion:{vcod}:{c}")
            w.writerow([fila_id, vehiculo_ids[vcod], carroceria_ids[c],
                        "true" if usa_cap else "false", cap_especifica, "seeder"])

# ---------- Tabla 5: categoriaMercancia ----------
# Catalogo cruzado con el seeder de Brayan (13 categorias, se adoptan las de
# el -- separa MATERIAL_INFLAMABLE/CARBURANTE donde antes teniamos un solo
# COMBUSTIBLE, y METALICO/CONSTRUCCION donde antes teniamos un solo
# METALICO_CONSTRUCCION; agrega QUIMICO_OXIDANTE y QUIMICO_CORROSIVO).
categorias = ["GENERAL", "ALIMENTOS", "FARMACEUTICO", "PERECEDERO_NO_ALIMENTO", "FERTILIZANTE",
              "MATERIAL_INFLAMABLE", "CARBURANTE", "METALICO", "CONSTRUCCION", "VIDRIO",
              "GAS_INDUSTRIAL", "QUIMICO_OXIDANTE", "QUIMICO_CORROSIVO"]
categoria_ids = {}
with open(f"{OUT}/categoriaMercancia.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "NombreCategoria", "CreadoPor"])
    for c in categorias:
        cid = nid(f"categoria:{c}")
        categoria_ids[c] = cid
        w.writerow([cid, c, "seeder"])

# ---------- Tabla 6: reglasIncompatibilidad ----------
# Reglas cruzadas con el seeder de Brayan -- se adoptan las 3 de el (la
# propia, Combustible x Alimentos, ya no aplica tal cual porque esa
# categoria se partio en MATERIAL_INFLAMABLE/CARBURANTE, ver Tabla 5).
# Pendiente de completar con operaciones (ver brecha #4 de
# propuesta-seleccion-vehiculos.md, seccion 3.3).
reglas_incompatibilidad = [
    ("FERTILIZANTE", "ALIMENTOS", "Riesgo de contaminacion de alimentos por fertilizante ensacado."),
    ("QUIMICO_OXIDANTE", "MATERIAL_INFLAMABLE", "Riesgo de reaccion / incendio sin separacion certificada."),
    ("QUIMICO_CORROSIVO", "ALIMENTOS", "Riesgo de contaminacion de alimentos."),
]
with open(f"{OUT}/reglasIncompatibilidad.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "IdCategoria1", "IdCategoria2", "Motivo", "CreadoPor"])
    for c1, c2, motivo in reglas_incompatibilidad:
        fila_id = nid(f"regla:{c1}:{c2}")
        w.writerow([fila_id, categoria_ids[c1], categoria_ids[c2], motivo, "seeder"])

# ---------- Tabla 3: tiposMercancia ----------
# Contenido cruzado con el seeder de Brayan (41 filas: nuestras 31
# originales + 10 "no cubicables" que el agrega -- granel, cemento,
# concreto, combustibles, asfalto, vehiculos en unidad, ingenieria
# especial). Carrocerias principal/secundaria siguen siendo las NUESTRAS
# (mas granulares); se resolvieron cruzando el generico de Brayan
# (FURGON/CONTENEDOR) con su propio campo ControlAmbientalRequerido --
# ej. FURGON + REFRIGERADO (sin condicional) -> FURGON_REFRIGERADO. Cuando
# el control es "_CONDICIONAL" o alternativo ("_O_"), se dejo la misma
# resolucion ya usada en la version anterior de esta tabla.
# campos: codigo, nombre, categoria, cubicable, condicionada,
#         carroceria_principal, carroceria_secundaria, secundaria_condicionada,
#         control_ambiental, apilable, fragil, rotacion_libre, comentario
filas = [
    ("CAJAS_CARTON", "Cajas de carton / mercancia empacada", "GENERAL", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Validar dimensiones, apilabilidad y resistencia del empaque."),
    ("ALIMENTOS_SECOS", "Alimentos secos empacados", "ALIMENTOS", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Producto empacado; verificar requisitos sanitarios."),
    ("ELECTRODOMESTICOS", "Electrodomesticos embalados", "GENERAL", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, True, None,
     "Proteger de lluvia, golpes y vibracion."),
    ("MUEBLES_ENSERES", "Muebles y enseres embalados", "MATERIAL_INFLAMABLE", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Validar piezas voluminosas y posibilidad de apilamiento."),
    ("FARMA_AMBIENTE", "Productos farmaceuticos a temperatura ambiente", "FARMACEUTICO", True, False,
     "FURGON_CLIMATIZADO", None, False, "CLIMATIZADO_CONDICIONAL", None, None, None,
     "Respetar condiciones declaradas por fabricante."),
    ("FARMA_REFRIGERADO", "Productos farmaceuticos refrigerados", "FARMACEUTICO", True, False,
     "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", True, "REFRIGERADO", None, None, None,
     "Definir rango de temperatura y monitoreo requerido."),
    ("ALIMENTOS_CONGELADOS", "Alimentos congelados empacados", "ALIMENTOS", True, False,
     "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", True, "REFRIGERADO", None, None, None,
     "Validar temperatura, ventilacion y estiba."),
    ("CARNICOS", "Carnes y productos carnicos empacados", "ALIMENTOS", True, False,
     "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", True, "REFRIGERADO", None, None, None,
     "Solo carga debidamente empacada; cumplir cadena de frio."),
    ("FRUTAS_VERDURAS", "Frutas y verduras empacadas", "ALIMENTOS", True, False,
     "FURGON_VENTILADO", "CONTENEDOR_REFRIGERADO", True, "VENTILADO_O_REFRIGERADO", None, None, None,
     "La temperatura y ventilacion dependen del producto."),
    ("FLORES_PERECEDEROS", "Flores y perecederos empacados", "PERECEDERO_NO_ALIMENTO", True, False,
     "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", True, "REFRIGERADO_O_CLIMATIZADO", None, None, None,
     "Definir temperatura y ventilacion segun especificacion."),
    ("LACTEOS", "Productos lacteos empacados", "ALIMENTOS", True, False,
     "FURGON_REFRIGERADO", "CONTENEDOR_REFRIGERADO", True, "REFRIGERADO", None, None, None,
     "Mantener cadena de frio."),
    ("BEBIDAS", "Bebidas embotelladas o enlatadas", "ALIMENTOS", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Considerar peso por pallet y resistencia del embalaje."),
    ("FERTILIZANTES_ENSACADOS", "Fertilizantes ensacados", "FERTILIZANTE", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Evitar humedad y contaminacion."),
    ("PALLETS_GENERAL", "Pallets de mercancia general", "GENERAL", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Cubicacion por pallet o unidad logistica; revisar espacios no aprovechables."),
    ("ESTIBAS_MADERA", "Estibas de madera (mercancia)", "MATERIAL_INFLAMABLE", True, False,
     "PLATAFORMA", "ESTACAS", False, "NINGUNO", None, None, None,
     "Cubicacion por dimensiones exteriores y forma de estiba."),
    ("MATERIALES_CONSTRUCCION", "Materiales de construccion empacados", "CONSTRUCCION", True, False,
     "ESTACAS", "FURGON", True, "NINGUNO", None, None, None,
     "Ej.: cajas o paquetes; excluir material suelto a granel."),
    ("LADRILLOS_BLOQUES", "Ladrillos y bloques paletizados", "CONSTRUCCION", True, False,
     "ESTACAS", "PLATAFORMA", False, "NINGUNO", None, None, None,
     "Validar peso por pallet y resistencia."),
    ("VARILLAS_PERFILES", "Varillas y perfiles metalicos en paquetes", "METALICO", True, False,
     "PLATAFORMA", "ESTACAS", False, "NINGUNO", None, None, False,
     "Cargas largas; validar longitud, amarres y salientes."),
    ("TUBERIAS", "Tuberias en paquetes", "METALICO", True, False,
     "PLATAFORMA", "ESTACAS", False, "NINGUNO", None, None, False,
     "Validar longitud, diametro y soportes."),
    ("VIGAS_ESTRUCTURAS", "Vigas y estructuras metalicas en piezas", "METALICO", True, True,
     "PLATAFORMA", None, False, "NINGUNO", None, None, None,
     "Si excede dimensiones ordinarias, tratar como carga especial."),
    ("LAMINAS_ACERO", "Laminas de acero en paquetes", "METALICO", True, False,
     "PLATAFORMA", "ESTACAS", True, "NINGUNO", None, None, None,
     "Verificar apoyos, proteccion de bordes y peso."),
    ("BOBINAS_ACERO", "Bobinas de acero", "METALICO", True, True,
     "PLATAFORMA", None, False, "NINGUNO", None, None, False,
     "Requiere plataforma con cunas portabobinas. La cubicacion exige orientacion, diametro, ancho y cunas."),
    ("MADERA_ASERRADA", "Madera aserrada en paquetes", "MATERIAL_INFLAMABLE", True, False,
     "PLATAFORMA", "ESTACAS", False, "NINGUNO", None, None, None,
     "Validar longitud, paquete y sujecion."),
    ("POSTES_CONCRETO", "Postes de concreto en unidades", "CONSTRUCCION", True, True,
     "PLATAFORMA", "ESTACAS", False, "NINGUNO", None, None, False,
     "Carga larga; validar apoyos y puntos de amarre."),
    ("PAPEL_CARTON", "Papel y carton en cajas o pacas", "MATERIAL_INFLAMABLE", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Proteger de humedad."),
    ("PAPEL_ROLLOS", "Papel en rollos", "MATERIAL_INFLAMABLE", True, True,
     "FURGON", "PLATAFORMA", False, "NINGUNO", None, None, False,
     "Definir diametro, ancho, orientacion y metodo de sujecion."),
    ("VIDRIO_PLANO", "Vidrio plano / laminas", "VIDRIO", True, True,
     "PORTAVIDRIOS", "PLATAFORMA", False, "NINGUNO", None, True, False,
     "Requiere bastidor, orientacion y proteccion especifica."),
    ("VENTANAS_FACHADAS", "Ventanas y fachadas embaladas", "VIDRIO", True, True,
     "FURGON", "PLATAFORMA", False, "NINGUNO", True, None, False,
     "Cubicacion por embalaje; controlar fragilidad y orientacion."),
    ("CILINDROS_GAS", "Cilindros de gases industriales", "GAS_INDUSTRIAL", True, True,
     "PLATAFORMA", "FURGON_VENTILADO", True, "VENTILADO_CONDICIONAL", None, None, False,
     "No cubicar como cajas comunes; validar clasificacion, segregacion y sujecion."),
    ("EXPORTACION_PALETIZADA", "Mercancia general de exportacion paletizada", "GENERAL", True, False,
     "FURGON", "ESTACAS", True, "NINGUNO", None, None, None,
     "Cubicacion por pallet/embalaje y patron de estiba. Tambien aplica contenedor seco."),
    ("CARGA_CONTENERIZADA", "Carga contenerizada en cajas o pallets", "GENERAL", True, False,
     "CONTENEDOR_SECO", "FURGON", True, "NINGUNO", None, None, None,
     "El contenedor es unidad de transporte; la cubicacion interna depende de medidas utiles y limites de carga."),
    # Nota: el seeder de Brayan trae 10 filas adicionales de carga NO
    # cubicable (granel, cemento, concreto, combustibles, asfalto, etc.) --
    # a proposito no se agregaron aqui, se dejaron fuera por decision
    # explicita (quedan solo las 31 de la Matriz/Excel).
]

def _b(valor):
    """None -> '' (NULL); True/False -> 'true'/'false'."""
    if valor is None:
        return ""
    return "true" if valor else "false"

def _tri(valor, default):
    """Para Apilable/Fragil/RotacionLibre: None toma el default de negocio
    (ya no quedan en NULL); un True/False explicito en la fila se respeta
    tal cual, el default solo aplica cuando no se dijo nada puntual."""
    if valor is None:
        valor = default
    return "true" if valor else "false"

with open(f"{OUT}/tiposMercancia.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "CodigoMercancia", "NombreMercancia", "IdCategoria", "Cubicable",
                "CondicionadaCubicacion", "ComentarioCondicion", "IdCarroceriaPrincipal",
                "IdCarroceriaSecundaria", "CarroceriaSecundariaCondicionada",
                "ControlAmbientalRequerido", "Apilable", "Fragil", "RotacionLibre",
                "Activo", "CreadoPor"])
    for codigo, nombre, cat, cubicable, cond, princ, sec, sec_cond, ctrl_amb, apil, frag, rot, com in filas:
        princ_id = carroceria_ids[princ] if princ else ""
        sec_id = carroceria_ids[sec] if sec else ""
        fila_id = nid(f"mercancia:{codigo}")
        w.writerow([fila_id, codigo, nombre, categoria_ids[cat], _b(cubicable), _b(cond),
                    com, princ_id, sec_id, _b(sec_cond), ctrl_amb,
                    _tri(apil, True), _tri(frag, False), _tri(rot, True),
                    "true", "seeder"])

# ---------- Tabla 9: parametrosMotor ----------
params = [
    ("k_max", "25", "Tope de vehiculos por grupo antes de sin seleccion automatica"),
    ("factor_vol_util", "0.90", "Fraccion del volumen util aprovechable en la practica"),
    ("usar_alternativa", "solo_respaldo", "Si la carroceria alternativa entra siempre o solo como respaldo"),
    ("umbral_alerta_llenado", "0.75", "Por debajo de este porcentaje de llenado se marca alerta"),
    ("margen_cg", "0.30", "Centro de gravedad dentro de +-30% del centro geometrico"),
]
with open(f"{OUT}/parametrosMotor.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "Clave", "Valor", "Descripcion", "CreadoPor"])
    for clave, valor, desc in params:
        w.writerow([nid(f"parametro:{clave}"), clave, valor, desc, "seeder"])

print("listo")
