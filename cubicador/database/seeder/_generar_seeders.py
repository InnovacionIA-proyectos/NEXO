# -*- coding: utf-8 -*-
"""Genera los 7 CSV del catálogo inicial del Cubicador.

Uso (desde esta carpeta):
    py _generar_seeders.py

Los datos del catálogo están definidos aquí, tabla por tabla. Para cambiar el
catálogo inicial se edita este archivo y se vuelve a correr; los CSV no se
editan a mano. El origen de cada dato y las reglas aplicadas están en
../documentacion/ORIGEN-Y-DECISIONES-SEEDER.md.

Cada fila recibe un Id determinístico (uuid5 sobre su clave de negocio): el
mismo registro produce siempre el mismo Id, en cualquier equipo y cualquier día.
Así todas las bases cargadas con estos CSV comparten los mismos Id, y
ejecutar_schema_y_seeders.py puede correr varias veces sin duplicar filas.
"""
import csv, uuid

# Namespace fijo del proyecto para los Id determinísticos. No cambiarlo: cambiaría
# todos los Id del catálogo.
_NAMESPACE_CUBICADOR = uuid.UUID("c9b1b1a0-6f3e-4b1a-9c2d-a1b2c3d40001")

def nid(seed: str) -> str:
    return str(uuid.uuid5(_NAMESPACE_CUBICADOR, seed))

OUT = "."

# ---------- vehiculos ----------
# Medidas en centímetros. El código es único porque se usa para cruzar con
# SICE-TAC y el tarifario; una capacidad distinta según la carrocería se define en
# relacionVehiculosCarroceria, no con un vehículo repetido. La altura es el límite
# legal de altura del tipo de vehículo (reglamentación de tránsito) y no depende de
# la carrocería.
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

# ---------- TiposCarroceria ----------
# Catálogo sin fusionar sinónimos: Plataforma y Planchón son códigos distintos (la
# matriz los usa como alternativas entre sí), y Furgón refrigerado, climatizado y
# ventilado también (son necesidades distintas).
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
    # Del catálogo anterior del motor de cubicación rápida.
    ("CAMA_BAJA", "Cama baja"),
    ("PORTACONTENEDORES", "Portacontenedores"),
    # Del catálogo de carrocerías de SAP (tblSAPTiposCarroseria).
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

# ---------- relacionVehiculosCarroceria ----------
# Qué carrocerías acepta cada vehículo, según el Excel «Dimensiones y Peso por
# vehículo» («Plancha» en el Excel corresponde a PLATAFORMA).
# Cada entrada: (carrocería, usa la capacidad del vehículo, capacidad específica).
# Única excepción: el 3S3 con Furgón carga 30.000 kg en vez de 34.000.
rel = {
    "2L2": [("ESTACAS", True, 0), ("FURGON", True, 0), ("PLATAFORMA", True, 0)],
    "C2": [("ESTACAS", True, 0), ("FURGON", True, 0), ("PLATAFORMA", True, 0)],
    "C3": [("ESTACAS", True, 0), ("PLATAFORMA", True, 0)],
    "2S2": [("ESTACAS", True, 0), ("FURGON", True, 0), ("PLATAFORMA", True, 0)],
    "3S3": [("ESTACAS", True, 0), ("PLATAFORMA", True, 0), ("FURGON", False, 30000)],
}
# Las variantes de Furgón (refrigerado, climatizado, ventilado) van en los mismos
# vehículos que llevan Furgón y con su misma capacidad: es el mismo chasis, solo
# cambia el acondicionamiento de la caja. El C3 no lleva Furgón.
for vcod, cs in list(rel.items()):
    for c, usa_cap, cap_especifica in cs:
        if c == "FURGON":
            for variante in ("FURGON_REFRIGERADO", "FURGON_CLIMATIZADO", "FURGON_VENTILADO"):
                rel[vcod].append((variante, usa_cap, cap_especifica))
            break
# Las carrocerías que no salen de ese Excel (CAMA_BAJA, PORTACONTENEDORES, TANQUE,
# TOLVA, VOLCO, GRUA, NINERA) no llevan relación: se asignan a un vehículo desde la
# administración del catálogo cuando haya un emparejamiento confirmado.
with open(f"{OUT}/relacionVehiculosCarroceria.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["Id", "IdTipoVehiculo", "IdCarroceria",
                "UsaCapacidadMaximaDelVehiculo", "CapacidadMaximaEspecifica", "CreadoPor"])
    for vcod, cs in rel.items():
        for c, usa_cap, cap_especifica in cs:
            fila_id = nid(f"relacion:{vcod}:{c}")
            w.writerow([fila_id, vehiculo_ids[vcod], carroceria_ids[c],
                        "true" if usa_cap else "false", cap_especifica, "seeder"])

# ---------- categoriaMercancia ----------
# Nivel al que se definen las incompatibilidades entre mercancías.
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

# ---------- reglasIncompatibilidad ----------
# Pares de categorías que no pueden viajar en el mismo vehículo. Son las reglas
# confirmadas hasta hoy; las demás se agregan desde la administración del catálogo.
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
        # El par se guarda con el Id menor primero (comparado como texto), igual que
        # lo hace la aplicación: así el UNIQUE impide la misma regla en los dos sentidos.
        id1, id2 = sorted([categoria_ids[c1], categoria_ids[c2]])
        w.writerow([fila_id, id1, id2, motivo, "seeder"])

# ---------- tiposMercancia ----------
# Las 31 mercancías de la Matriz cubicador. La carrocería se elige según el control
# ambiental: por ejemplo, una mercancía refrigerada va en FURGON_REFRIGERADO.
# Apilable, Fragil y RotacionLibre en None toman su valor por defecto (ver _tri).
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
]

def _b(valor):
    """None -> '' (NULL); True/False -> 'true'/'false'."""
    if valor is None:
        return ""
    return "true" if valor else "false"

def _tri(valor, default):
    """Apilable, Fragil y RotacionLibre: None toma el valor por defecto; un
    True/False explícito en la fila se respeta."""
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

# ---------- parametrosMotor ----------
# k_max = 25: con 10 se rechazaban cargas con solución (600 piezas de 1 m necesitan
# 13 vehículos) y con 25 el cálculo sigue tardando centésimas de segundo.
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
