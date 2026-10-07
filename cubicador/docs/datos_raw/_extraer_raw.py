# -*- coding: utf-8 -*-
"""Vuelca el contenido crudo de los 3 Excel fuente a CSV, sin normalizar ni
interpretar nada -- tal cual estan en el archivo original. Sirve de respaldo
para auditar las decisiones tomadas en cubicador/docs/seeder/."""
import csv
import openpyxl

ORIGEN = ".."


def volcar(nombre_archivo, nombre_hoja, salida):
    wb = openpyxl.load_workbook(f"{ORIGEN}/{nombre_archivo}", data_only=True)
    ws = wb[nombre_hoja]
    with open(salida, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        for row in ws.iter_rows(values_only=True):
            w.writerow(["" if v is None else v for v in row])


volcar("Dimenciones y Peso por configuracion para Cubicaje.xlsx",
       "Dimenciones y Pesos Cubicaje", "dimensiones_y_peso_por_vehiculo_RAW.csv")

volcar("Matriz_cubicador_mercancia_mercancias vs carrocerias.xlsx",
       "Matriz cubicador", "matriz_cubicador_mercancia_vs_carroceria_RAW.csv")

volcar("Matriz_cubicador_mercancia_mercancias vs carrocerias.xlsx",
       "Alcance y exclusiones", "matriz_cubicador_alcance_y_exclusiones_RAW.csv")

volcar("Diccionario de Configuraciones de vehiculos para Nexo.xlsx",
       "Diccionario configuraciones", "diccionario_configuraciones_vehiculo_RAW.csv")

print("listo")
