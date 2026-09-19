using System;
using Nexo.Domain.Catalogos.Entidades;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class clsCubicador3D : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehiculos_cubicacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LargoInternoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AnchoInternoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AltoInternoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CapacidadKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TaraKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PuertaAnchoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PuertaAltoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PuertaPosicion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EjesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToleranciaParedesMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ToleranciaEntreUnidadesMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    EsPreset = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehiculos_cubicacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "planes_cargue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estrategia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PonderacionesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Algoritmo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Semilla = table.Column<int>(type: "int", nullable: true),
                    PesoTotalKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    VolumenUtilizadoM3 = table.Column<decimal>(type: "decimal(12,3)", nullable: false),
                    UtilizacionVolumen = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    UtilizacionPiso = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Puntaje = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    IncidenciasJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planes_cargue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_planes_cargue_vehiculos_cubicacion_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "vehiculos_cubicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items_plan_cargue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCargueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Pedido = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    clsCliente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Destino = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SecuenciaEntrega = table.Column<int>(type: "int", nullable: false),
                    LargoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AnchoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AltoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PesoKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    TipoEmbalaje = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Apilable = table.Column<bool>(type: "bit", nullable: false),
                    MaxNiveles = table.Column<int>(type: "int", nullable: false),
                    MaxPesoEncimaKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Rotable = table.Column<bool>(type: "bit", nullable: false),
                    MantenerVertical = table.Column<bool>(type: "bit", nullable: false),
                    Fragil = table.Column<bool>(type: "bit", nullable: false),
                    Peligroso = table.Column<bool>(type: "bit", nullable: false),
                    Refrigerado = table.Column<bool>(type: "bit", nullable: false),
                    GrupoCompatibilidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items_plan_cargue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_items_plan_cargue_planes_cargue_PlanCargueId",
                        column: x => x.PlanCargueId,
                        principalTable: "planes_cargue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "colocaciones_cargue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCargueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemPlanCargueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndiceUnidad = table.Column<int>(type: "int", nullable: false),
                    Cargada = table.Column<bool>(type: "bit", nullable: false),
                    X = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Y = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Z = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    LargoColocadoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AnchoColocadoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AltoColocadoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    OrientacionIndice = table.Column<int>(type: "int", nullable: false),
                    SecuenciaCargue = table.Column<int>(type: "int", nullable: false),
                    SoporteColocacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PorcentajeSoporte = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PesoEncimaKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    EstadoEstabilidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Accesibilidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Bloqueada = table.Column<bool>(type: "bit", nullable: false),
                    AjusteManual = table.Column<bool>(type: "bit", nullable: false),
                    CodigoRechazo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RazonRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_colocaciones_cargue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_colocaciones_cargue_items_plan_cargue_ItemPlanCargueId",
                        column: x => x.ItemPlanCargueId,
                        principalTable: "items_plan_cargue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_colocaciones_cargue_planes_cargue_PlanCargueId",
                        column: x => x.PlanCargueId,
                        principalTable: "planes_cargue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_colocaciones_cargue_ItemPlanCargueId",
                table: "colocaciones_cargue",
                column: "ItemPlanCargueId");

            migrationBuilder.CreateIndex(
                name: "IX_colocaciones_cargue_PlanCargueId_Cargada",
                table: "colocaciones_cargue",
                columns: new[] { "PlanCargueId", "Cargada" });

            migrationBuilder.CreateIndex(
                name: "IX_items_plan_cargue_PlanCargueId",
                table: "items_plan_cargue",
                column: "PlanCargueId");

            migrationBuilder.CreateIndex(
                name: "IX_planes_cargue_VehiculoId",
                table: "planes_cargue",
                column: "VehiculoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "colocaciones_cargue");

            migrationBuilder.DropTable(
                name: "items_plan_cargue");

            migrationBuilder.DropTable(
                name: "planes_cargue");

            migrationBuilder.DropTable(
                name: "vehiculos_cubicacion");
        }
    }
}
