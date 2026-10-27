using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Reconoce un nombre de persona escrito en una celda cualquiera. Es la segunda defensa del enmascarado: la primera mira el encabezado, y un
/// encabezado que no delata nada ("Quien recibe", "Col 7") dejaba salir los nombres tal cual.
///
/// No hay manera de reconocer TODOS los nombres con reglas, así que esto no pretende ser exacto sino cubrir lo corriente sin ocultar de más:
/// una ciudad, un vehículo o una mercancía de dos palabras ("Santa Marta", "Camión Sencillo") tienen que pasar. Por eso el criterio no es la
/// forma ("dos palabras con mayúscula") sino que alguna de las palabras sea un nombre o un apellido conocido, y que la celda no empiece con
/// el prefijo de un lugar ("San", "Santa", "Puerto"). El que se queda sin reconocer lo recoge el enmascarado por columna: ver
/// <see cref="clsEnmascaradorDeCeldas.MarcadorPorContenido"/>.
/// </summary>
internal static class clsNombresDePersona
{
    private static readonly Regex Palabra = new(@"^[A-Z][A-Z'’]*\.?$", RegexOptions.Compiled);

    private static readonly HashSet<string> Tratamientos = ["SR", "SRA", "SRES", "SRTA", "DON", "DONA", "DR", "DRA", "ING", "LIC", "ARQ", "ABG", "MR", "MRS"];
    private static readonly HashSet<string> Conectores = ["DE", "DEL", "LA", "LAS", "LOS", "Y", "E", "DA"];
    // "San Juan", "Santa Marta", "Puerto Colombia": los santos y los puertos son lugares, aunque lleven un nombre de pila.
    private static readonly HashSet<string> PrefijosDeLugar = ["SAN", "SANTA", "SANTO", "PUERTO", "VILLA"];

    // Normalizados (mayúsculas, sin tildes). Los más comunes en Colombia: lo que no esté aquí lo recoge el enmascarado por columna.
    private static readonly HashSet<string> Conocidos = new(
        ("JUAN JOSE LUIS CARLOS JORGE ANDRES CAMILO DAVID DANIEL MIGUEL JAVIER FERNANDO ALEJANDRO SEBASTIAN SANTIAGO FELIPE NICOLAS DIEGO OSCAR " +
         "EDWIN WILLIAM JHON JOHN FABIAN CESAR MAURICIO RICARDO ALBERTO ALEXANDER ALEX EDGAR HERNAN HUGO JAIME JESUS MARIO MARCO MARTIN NELSON " +
         "ORLANDO PABLO PEDRO RAFAEL RAUL ROBERTO RODRIGO SERGIO VICTOR YEISON JHONATAN JONATHAN CRISTIAN CHRISTIAN BRAYAN BRIAN KEVIN STEVEN " +
         "STIVEN DUVAN HECTOR HENRY GUSTAVO GERMAN GILBERTO GIOVANNY GIOVANNI FREDY FREDDY FRANCISCO FABIO ERNESTO ENRIQUE EDUARDO " +
         "DARIO CRISTOBAL CLAUDIO BERNARDO ARMANDO ARTURO ANTONIO ANGEL AMILCAR ALVARO ALFONSO ALFREDO ADOLFO ABEL ADRIAN IVAN JAIRO LEONARDO " +
         "LUCAS MANUEL MATEO NESTOR OMAR OSWALDO REINALDO SAMUEL SANTOS TOMAS WILSON WILMER YEFERSON " +
         "MARIA ANA CLAUDIA SANDRA PATRICIA MARTHA DIANA PAOLA LAURA CAROLINA ANGELA ADRIANA ALEJANDRA ANDREA CATALINA DANIELA JULIANA " +
         "LILIANA LUCIA MONICA NATALIA SOFIA VALENTINA YENNY YOLANDA ELIZABETH BEATRIZ BLANCA CECILIA CLARA DORA " +
         "ELENA ELSA EMILIA ESTELA GINA GLADYS INES ISABEL JANETH JENNIFER JESSICA JOHANA JOHANNA KAREN KATHERINE LEIDY LORENA LUCERO " +
         "MARCELA MARGARITA MARIANA MARISOL MARLENY MAYERLI MELISSA MILENA NANCY NUBIA OLGA RUTH SARA SANDY SHIRLEY SILVIA " +
         "STEFANY STEPHANIE SUSANA TATIANA VANESSA VERONICA VIVIANA XIMENA YAMILE YANETH YESENIA YULI YULIANA " +
         "PEREZ GOMEZ RODRIGUEZ MARTINEZ GARCIA LOPEZ GONZALEZ HERNANDEZ RAMIREZ SANCHEZ TORRES DIAZ VARGAS CASTRO ROJAS MORENO JIMENEZ RUIZ " +
         "GUTIERREZ ORTIZ MUNOZ ALVAREZ ROMERO SUAREZ RAMOS MEDINA CARDENAS ACOSTA AGUILAR ARIAS AVILA BARRERA BELTRAN BENITEZ BERMUDEZ " +
         "BOTERO BUITRAGO CABRERA CALDERON CAMACHO CARDONA CARVAJAL CASTILLO CASTANO CELIS CORTES CRUZ CUELLAR DELGADO DUARTE DUQUE ESCOBAR " +
         "FIGUEROA FLOREZ FONSECA FORERO FUENTES GALEANO GALINDO GALVIS GAMBOA GIL GIRALDO GOMES GUERRERO GUZMAN HENAO HERRERA HOYOS " +
         "JARAMILLO LEON LONDONO LOZANO MARIN MARQUEZ MEJIA MENDEZ MENDOZA MOLINA MONTOYA MORALES NARANJO NAVARRO NIETO NINO OCAMPO " +
         "OLARTE ORTEGA OSORIO OSPINA PACHECO PADILLA PARRA PINEDA PINZON POSADA PRIETO QUINTERO QUINONES RAMON RESTREPO REYES RINCON " +
         "RIOS RIVERA ROA ROBLES RUBIO SALAZAR SALGADO SANDOVAL SANTAMARIA SERRANO SILVA SOTO TAMAYO TOBON TRUJILLO URIBE VALENCIA " +
         "VALDERRAMA VALLEJO VASQUEZ VEGA VELASQUEZ VELEZ VERA VILLAMIL ZAMBRANO ZAPATA ZULUAGA").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>¿La celda parece un nombre de persona (o una frase que menciona uno)?</summary>
    public static bool Parece(string? celda)
    {
        // "Pérez, Juan": la coma y los dos puntos no cambian de quién se habla.
        var palabras = clsResolucionDeCatalogos.Normalizar(celda).Split([' ', ',', ';', ':'], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (palabras.Count == 0) return false;

        // "Sr. Ruiz", "Ing. Carlos Ruiz": un tratamiento delante delata a una persona, se conozca el nombre o no.
        var conTratamiento = Tratamientos.Contains(palabras[0].TrimEnd('.'));
        if (conTratamiento) palabras.RemoveAt(0);
        if (palabras.Count == 0) return false;
        if (conTratamiento && palabras.All(p => Palabra.IsMatch(p))) return true;

        if (PrefijosDeLugar.Contains(palabras[0])) return false;

        // La celda entera es un nombre: de dos a seis palabras, solo letras, y alguna conocida.
        var significativas = palabras.Where(p => !Conectores.Contains(p)).ToList();
        if (palabras.Count <= 6 && significativas.Count >= 2 && palabras.All(p => Palabra.IsMatch(p)) && significativas.Any(EsConocida))
            return true;

        // Una frase que nombra a alguien ("Entregar a Juan Pérez en bodega 3"): dos palabras conocidas seguidas.
        for (var i = 0; i + 1 < palabras.Count; i++)
        {
            if (PrefijosDeLugar.Contains(palabras[i])) continue;
            if (EsConocida(palabras[i]) && EsConocida(palabras[i + 1])) return true;
        }
        return false;
    }

    private static bool EsConocida(string palabra) => Conocidos.Contains(palabra.TrimEnd('.'));
}
