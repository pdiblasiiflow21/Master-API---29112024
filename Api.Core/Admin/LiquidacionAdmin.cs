﻿using Api.Core.Dtos.Common;
using Api.Core.Dtos.ErpMilonga;
using Api.Core.Dtos.Filter;
using Api.Core.Dtos.Request;
using Api.Core.Dtos.Response;
using Api.Core.Entities;
using Api.Core.Enums;
using Api.Core.Helpers;
using Api.Core.Services.Interfaces;
using AutoMapper.QueryableExtensions.Impl;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Api.Core.Admin
{
    public class LiquidacionAdmin : BaseAdmin<int, Entities.Liquidacion, Dtos.Liquidacion, FilterLiquidacion>
    {
        public IErpMilongaInvoiceService ErpMilongaInvoiceService;

        public override IQueryable GetQuery(FilterLiquidacion filter)
        {
            var query = MyContext.Liquidaciones
                .Include(x => x.Conceptos)
                    .ThenInclude(c => c.Concepto)
                 .Include(x => x.Cliente).ThenInclude(x => x.TipoDocumento)
                                                      .Include(x => x.Cliente).ThenInclude(x => x.TipoImpuesto)
                                                      .Include(x => x.Cliente).ThenInclude(x => x.Localidad)
                                                      .Include(x => x.Cliente).ThenInclude(x => x.CondicionPago)
                 .Include(x => x.DetalleLiquidacionPre)
                 .Include(x => x.DetalleLiquidacionPos)
                 .OrderByDescending(x => x.CreateDate)
                 .AsNoTracking();

            query = query.Where(e =>
                !e.Deleted &&
                (filter.ClienteId == null || e.IdCliente == filter.ClienteId) &&
                (filter.FechaDesde == null || e.CreateDate.Date >= filter.FechaDesde.Value.Date) &&
                (filter.FechaHasta == null || e.CreateDate.Date <= filter.FechaHasta.Value.Date) &&
                ((filter.Estado == null || filter.Estado == 0) || e.Estado == filter.Estado) &&
                (string.IsNullOrEmpty(filter.MultiColumnSearchText) || e.Descripcion.Contains(filter.MultiColumnSearchText, StringComparison.OrdinalIgnoreCase)));

            return query;
        }

        public async Task<int> AddComprobante(int id, Dtos.ComprobantePost request)
        {
            var entity = await MyContext.Liquidaciones.Include(MyContext.GetIncludePaths(typeof(Liquidacion))).SingleAsync(e => e.Id == id);

            using (var memoryStream = new MemoryStream())
            {
                request.Archivo.CopyTo(memoryStream);

                entity.Comprobantes.Add(new Comprobante
                {
                    Nombre = request.Archivo.FileName,
                    Archivo = memoryStream.ToArray(),
                    Posicion = request.Posicion,
                    Enabled = true,
                    CreateDate = DateTime.Now,
                    CreatedBy = CurrentUser?.user_id
                });
            }

            return await MyContext.SaveChangesAsync();
        }

        public async Task RemoveComprobante(int id, int archivoId)
        {
            var entity = await MyContext.Liquidaciones.Include(MyContext.GetIncludePaths(typeof(Liquidacion))).SingleAsync(e => e.Id == id);

            var comprobanteToRemove = entity.Comprobantes.First(x => x.Id == archivoId);

            MyContext.Comprobantes.Remove(comprobanteToRemove);

            await MyContext.SaveChangesAsync();
        }

        public async Task<Dtos.ComprobanteDownload> GetComprobante(int id, int archivoId)
        {
            var entity = await MyContext.Liquidaciones.Include(MyContext.GetIncludePaths(typeof(Liquidacion))).SingleAsync(e => e.Id == id);

            var comprobante = entity.Comprobantes.First(x => x.Id == archivoId);

            return new Dtos.ComprobanteDownload
            {
                Nombre = comprobante.Nombre,
                Contenido = comprobante.Archivo
            };
        }

        public async Task<List<Dtos.Comprobante>> GetAllComprobantes(int id)
        {
            var entity = await MyContext.Liquidaciones.Include(MyContext.GetIncludePaths(typeof(Liquidacion))).SingleAsync(e => e.Id == id);

            return Mapper.Map<List<Comprobante>, List<Dtos.Comprobante>>(entity.Comprobantes.ToList());
        }

        public async Task<List<Dtos.LiquidacionReport>> GetAllForReportAsync(IList<int> liquidacionesIds)
        {
            var liquidaciones = await MyContext.Liquidaciones.AsNoTracking()
                .Include(MyContext.GetIncludePaths(typeof(Liquidacion)))
                .Where(x => liquidacionesIds.Contains(x.Id))
                .ToListAsync();

            return liquidaciones.SelectMany(x => x.Conceptos.Select(y => new Dtos.LiquidacionReport
            {
                Id = x.Id,
                Fecha = x.CreateDate.ToString("dd/MM/yyyy"),
                Cliente = x.Cliente?.RazonSocialNombre ?? string.Empty,
                Descripcion = x.Descripcion,
                Saldo = x.Saldo,
                Estado = EnumHelper.GetDescription(x.Estado),
                Factura = x.Factura,
                OtrosComprobantes = x.OtrosComprobantes,
                NumeroFactura = x.NumeroFactura,
                MontoTotalImpuestos = x.MontoTotalImpuestos,
                MontoFinalFactura = x.MontoFinalFactura,
                ConceptoObservacion = y.Observacion,
                ConceptoMonto = y.Monto
            })).ToList();
        }

        public async Task<List<Dtos.LiquidacionReport>> GetAllForReportAsync(ExportAllLiquidacionesRequest request)
        {
            var liquidaciones = await MyContext.Liquidaciones.AsNoTracking()
                .Include(MyContext.GetIncludePaths(typeof(Liquidacion)))
                .Where(x => !x.Deleted &&
                    (request.ClienteId == null || x.IdCliente == request.ClienteId) &&
                    (request.FechaDesde == null || x.CreateDate.Date >= request.FechaDesde.Value.Date) &&
                    (request.FechaHasta == null || x.CreateDate.Date <= request.FechaHasta.Value.Date) &&
                    (request.EstadoFilter == null || request.EstadoFilter == 0 || x.Estado == request.EstadoFilter) &&
                    (string.IsNullOrEmpty(request.Search) || x.Descripcion.Contains(request.Search, StringComparison.OrdinalIgnoreCase)) &&
                    (request.UncheckedIds == null || request.UncheckedIds.Length == 0 || !request.UncheckedIds.Contains(x.Id)))
                .ToListAsync();

            return liquidaciones.SelectMany(x => x.Conceptos.Select(y => new Dtos.LiquidacionReport
            {
                Id = x.Id,
                Fecha = x.CreateDate.ToString("dd/MM/yyyy"),
                Cliente = x.Cliente?.RazonSocialNombre ?? string.Empty,
                Descripcion = x.Descripcion,
                Saldo = x.Saldo,
                Estado = EnumHelper.GetDescription(x.Estado),
                Factura = x.Factura,
                OtrosComprobantes = x.OtrosComprobantes,
                NumeroFactura = x.NumeroFactura,
                MontoTotalImpuestos = x.MontoTotalImpuestos,
                MontoFinalFactura = x.MontoFinalFactura,
                ConceptoObservacion = y.Observacion,
                ConceptoMonto = y.Monto
            })).ToList();
        }

        public async Task AddConceptoAsync(int id, ConceptoLiquidacionRequest request)
        {
            var liquidacion = await MyContext.Liquidaciones
                .Include(MyContext.GetIncludePaths(typeof(Liquidacion)))
                .SingleOrDefaultAsync(x => x.Id == id);

            if (liquidacion == null)
                throw new Exception($"No existe liquidación con ID: {id}");

            var concepto = await MyContext.Conceptos
                .SingleOrDefaultAsync(x => x.Id == request.Concepto.Id);

            if (concepto == null)
                throw new Exception($"No existe concepto con ID: {request.Concepto.Id}");

            liquidacion.Saldo += request.Monto;

            liquidacion.Conceptos.Add(new ConceptoLiquidacion
            {
                ConceptoId = concepto.Id,
                Monto = request.Monto,
                CreateDate = DateTime.Now,
                Observacion = request.Observacion,
                Estado = EstadoConceptoCliente.Autorizado,
                CreatedBy = CurrentUser?.user_id ?? "Admin",
                Enabled = true
            });

            await MyContext.SaveChangesAsync();
        }

        public async Task AnularConceptoAsync(int id, int conceptoId)
        {
            var liquidacion = await MyContext.Liquidaciones
                .Include(MyContext.GetIncludePaths(typeof(Liquidacion)))
                .SingleOrDefaultAsync(x => x.Id == id);

            if (liquidacion == null)
                throw new Exception($"No existe liquidación con ID: {id}");

            var conceptoLiquidacionToDelete = liquidacion.Conceptos.FirstOrDefault(x => x.Id == conceptoId);

            if (conceptoLiquidacionToDelete == null)
                throw new Exception($"No existe ningún concepto con ID {conceptoId} asociado a la liquidación {id}");

            liquidacion.Saldo -= conceptoLiquidacionToDelete.Monto;

            if (conceptoLiquidacionToDelete.ConceptoClienteId.HasValue)
            {
                var conceptoCliente = await MyContext.ConceptosClientes
                    .SingleOrDefaultAsync(x => x.Id == conceptoLiquidacionToDelete.ConceptoClienteId.Value);

                conceptoCliente.Estado = EstadoConceptoCliente.PendienteDeAutorizacion;
            }

            MyContext.Remove(conceptoLiquidacionToDelete);


            await MyContext.SaveChangesAsync();
        }

        public override Liquidacion ToEntity(Dtos.Liquidacion dto)
        {
            var entity = new Entities.Liquidacion();
            bool recalculateSaldo = false;

            if (dto.Id.HasValue)
            {
                entity = MyContext.Liquidaciones
                    .Include(x => x.Conceptos)
                        .ThenInclude(x => x.Concepto)
                    .Single(e => e.Id == dto.Id.Value);
            }

            entity.Descripcion = dto.Descripcion;

            foreach (var currentConcepto in entity.Conceptos)
            {
                if (currentConcepto.Concepto.IsGeneric)
                    continue;

                var dtoConcepto = dto.Conceptos.FirstOrDefault(x => x.ConceptoId == currentConcepto.ConceptoId);

                if (dtoConcepto == null)
                    continue;

                if (currentConcepto.Monto != dtoConcepto.Monto)
                {
                    currentConcepto.Monto = dtoConcepto.Monto;
                    currentConcepto.UpdatedBy = CurrentUser?.user_id ?? "Admin";
                    currentConcepto.UpdateDate = DateTime.Now;

                    recalculateSaldo = true;
                }
            }

            var conceptosToAdd = dto.Conceptos.Where(x => !entity.Conceptos.Any(y => y.ConceptoId == x.ConceptoId)).ToList();

            var conceptosToCancel = entity.Conceptos.Where(x => !dto.Conceptos.Any(y => y.ConceptoId == x.ConceptoId)).ToList();

            if (conceptosToAdd.Any() || conceptosToCancel.Any())
            {
                recalculateSaldo = true;

                conceptosToAdd.ForEach(x =>
                {
                    entity.Conceptos.Add(new ConceptoLiquidacion
                    {
                        ConceptoId = x.ConceptoId,
                        Monto = x.Monto,
                        CreateDate = DateTime.Now,
                        Observacion = x.Observacion,
                        Estado = EstadoConceptoCliente.Autorizado,
                        CreatedBy = CurrentUser?.user_id ?? "Admin",
                        Enabled = true
                    });
                });

                var conceptoClienteIdsToUpdate = conceptosToCancel.Where(x => x.ConceptoClienteId.HasValue)
                    .Select(x => x.ConceptoClienteId)
                    .ToList();

                var conceptosClienteToUpdate = MyContext.ConceptosClientes.Where(x => conceptoClienteIdsToUpdate.Contains(x.Id)).ToList();

                conceptosClienteToUpdate.ForEach(x => x.Estado = EstadoConceptoCliente.PendienteDeAutorizacion);

                foreach (var conceptoToCancel in conceptosToCancel)
                {
                    conceptoToCancel.Estado = EstadoConceptoCliente.Anulado;
                    conceptoToCancel.UpdateDate = DateTime.Now;
                    conceptoToCancel.UpdatedBy = CurrentUser?.user_id ?? "Admin";
                }
            }

            if (recalculateSaldo)
                entity.Saldo = CalculateSaldo(entity.Conceptos);

            return entity;
        }

        private decimal CalculateSaldo(IList<ConceptoLiquidacion> conceptosLiquidacion)
        {
            decimal saldo = 0;

            var conceptosLiquidacionAutorizados = conceptosLiquidacion.Where(x => x.Estado == EstadoConceptoCliente.Autorizado).ToList();

            var conceptosIds = conceptosLiquidacionAutorizados.Select(x => x.ConceptoId).ToList();

            var conceptos = MyContext.Conceptos.Where(x => conceptosIds.Contains(x.Id)).ToList();

            foreach (var conceptoLiquidacion in conceptosLiquidacionAutorizados)
            {
                saldo += !conceptos.First(x => x.Id == conceptoLiquidacion.ConceptoId).Descuento ? conceptoLiquidacion.Monto : -conceptoLiquidacion.Monto;
            }

            return saldo;
        }

        public override Dtos.Response.ResultMessage<Dtos.Liquidacion> Validate(Dtos.Liquidacion dto, int Type)
        {
            // Type: 1-Create, 2-Update, 3-Delete 
            var entity = new Entities.Liquidacion();
            Dtos.Response.ResultMessage<Dtos.Liquidacion> respuesta_resultado = new Dtos.Response.ResultMessage<Dtos.Liquidacion>() { Content = dto, Successful = true, Message = "OK" };

            switch (Type)
            {
                case 1:
                    break;
                case 2:
                    break;
                case 3:
                    break;
                default:
                    break;
            }

            return respuesta_resultado;
        }

        public async Task UpdateAllStatus(UpdateAllLiquidacionesRequest request)
        {
            var liquidaciones = await MyContext.Liquidaciones
                .Include(MyContext.GetIncludePaths(typeof(Liquidacion)))
                .Where(x => x.Estado == EstadoLiquidacion.PendienteDeAutorizacion && 
                    !x.Deleted &&
                    (request.ClienteId == null || x.IdCliente == request.ClienteId) &&
                    (request.FechaDesde == null || x.CreateDate.Date >= request.FechaDesde.Value.Date) &&
                    (request.FechaHasta == null || x.CreateDate.Date <= request.FechaHasta.Value.Date) &&
                    (string.IsNullOrEmpty(request.Search) || x.Descripcion.Contains(request.Search, StringComparison.OrdinalIgnoreCase)) &&
                    (request.UncheckedIds == null || request.UncheckedIds.Length == 0 || !request.UncheckedIds.Contains(x.Id)))
                .ToListAsync();

            if (request.Estado == EstadoLiquidacion.Autorizada)
            {
                var liquidacionesToUpdate = liquidaciones.Where(x => x.Saldo > 0).ToList();

                foreach (var liquidacionToUpdate in liquidacionesToUpdate)
                {
                    liquidacionToUpdate.Estado = EstadoLiquidacion.Autorizada;
                    liquidacionToUpdate.UpdateDate = DateTime.Now;
                    liquidacionToUpdate.UpdatedBy = CurrentUser?.user_id;
                }

                await MyContext.SaveChangesAsync();
            }
            else if (request.Estado == EstadoLiquidacion.Cancelada)
            {
                foreach (var liquidacion in liquidaciones)
                {
                    liquidacion.Estado = EstadoLiquidacion.Cancelada;

                    var conceptoClienteIds = liquidacion.Conceptos.Where(x => x.ConceptoClienteId.HasValue)
                        .Select(x => x.ConceptoClienteId.Value)
                        .ToList();

                    if (conceptoClienteIds.Any())
                    {
                        var conceptosClienteToUpdate = MyContext.ConceptosClientes
                            .Where(x => conceptoClienteIds.Contains(x.Id))
                            .ToList();

                        conceptosClienteToUpdate.ForEach(x => x.Estado = EstadoConceptoCliente.PendienteDeAutorizacion);
                    }

                    var ordenes = liquidacion.DetalleLiquidacionPre.Where(x => !x.Deleted).ToList();

                    if (ordenes.Count > 0)
                    {
                        foreach (var orden in ordenes)
                        {
                            orden.Estado = EstadoItem.PendienteLiquidar;
                        }

                    }

                    var envios = liquidacion.DetalleLiquidacionPos.Where(x => !x.Deleted).ToList();

                    if (envios.Count > 0)
                    {
                        foreach (var envio in envios)
                        {
                            envio.Estado = EstadoItem.PendienteLiquidar;
                        }

                    }

                    liquidacion.UpdateDate = DateTime.Now;
                    liquidacion.UpdatedBy = CurrentUser?.user_id;
                }

                await MyContext.SaveChangesAsync();
            }
        }

        public async Task<ErpInvoiceCreateResponse> SendAllLiquidacionesToErp(SendAllToErpRequest request)
        {
            try
            {
                var liquidaciones = await MyContext.Liquidaciones
               .Include(x => x.DetalleLiquidacionPre)
               .Include(x => x.DetalleLiquidacionPre)
               .Include(x => x.Conceptos)
                   .ThenInclude(c => c.Concepto)
                   .ThenInclude(c => c.CodigoProducto)
               .Include(x => x.Cliente)
                   .ThenInclude(x => x.TipoDocumento)
               .Include(x => x.Cliente)
                   .ThenInclude(x => x.TipoImpuesto)
               .Include(x => x.Cliente)
                   .ThenInclude(x => x.Localidad)
                   .ThenInclude(x => x.Provincia)
               .Include(x => x.Cliente)
                   .ThenInclude(x => x.CondicionPago)
               .Include(x => x.Cliente)
                   .ThenInclude(x => x.Impuestos)
                   .ThenInclude(x => x.Impuesto)
                .Where(x => x.Estado == EstadoLiquidacion.Autorizada &&
                   !x.Deleted &&
                   (request.ClienteId == null || x.IdCliente == request.ClienteId) &&
                   (request.FechaDesde == null || x.CreateDate.Date >= request.FechaDesde.Value.Date) &&
                   (request.FechaHasta == null || x.CreateDate.Date <= request.FechaHasta.Value.Date) &&
                   (string.IsNullOrEmpty(request.Search) || x.Descripcion.Contains(request.Search, StringComparison.OrdinalIgnoreCase)) &&
                   (request.UncheckedIds == null || request.UncheckedIds.Length == 0 || !request.UncheckedIds.Contains(x.Id)))
                .ToListAsync();

                return await SendToErp(liquidaciones);
            }
            catch (Exception ex)
            {

                throw;
            }
           
        }

        public LiquidacionUpdateStatusResponse UpdateStatus(UpdateStatusLiquidacionRequest updateStatusLiquidacion)
        {
            int estado = updateStatusLiquidacion.EstadoId;

            var response = new LiquidacionUpdateStatusResponse();
            foreach (var id in updateStatusLiquidacion.Ids)
            {
                var entity = new Entities.Liquidacion();

                entity = MyContext.Liquidaciones.Include(MyContext.GetIncludePaths(typeof(Liquidacion))).FirstOrDefault(x => x.Id == id);

                if (entity == null)
                    continue;

                if (EstadoLiquidacion.PendienteDeAutorizacion.Equals(entity.Estado))
                {
                    if ((int)EstadoLiquidacion.Autorizada == estado)
                    {
                        if (entity.Saldo > 0)
                        {
                            entity.Estado = EstadoLiquidacion.Autorizada;
                            entity.UpdateDate = DateTime.Now;
                            entity.UpdatedBy = CurrentUser?.user_id;

                            MyContext.SaveChanges();
                        }
                        else
                        {
                            response.Code = 400;
                            response.Message = "No se puede autorizar la liquidacion porque no tiene saldo positivo";
                            return response;
                        }

                    }
                    if ((int)EstadoLiquidacion.Cancelada == estado)
                    {
                        entity.Estado = EstadoLiquidacion.Cancelada;

                        var conceptoClienteIds = entity.Conceptos.Where(x => x.ConceptoClienteId.HasValue)
                            .Select(x => x.ConceptoClienteId.Value)
                            .ToList();

                        if (conceptoClienteIds.Any())
                        {
                            var conceptosClienteToUpdate = MyContext.ConceptosClientes
                                .Where(x => conceptoClienteIds.Contains(x.Id))
                                .ToList();

                            conceptosClienteToUpdate.ForEach(x => x.Estado = EstadoConceptoCliente.PendienteDeAutorizacion);
                        }

                        var ordenes = entity.DetalleLiquidacionPre.Where(x => !x.Deleted).ToList();

                        if (ordenes.Count > 0)
                        {
                            foreach (var orden in ordenes)
                            {
                                orden.Estado = EstadoItem.PendienteLiquidar;
                            }

                        }

                        var envios = entity.DetalleLiquidacionPos.Where(x => !x.Deleted).ToList();

                        if (envios.Count > 0)
                        {
                            foreach (var envio in envios)
                            {
                                envio.Estado = EstadoItem.PendienteLiquidar;
                            }

                        }

                        entity.UpdateDate = DateTime.Now;
                        entity.UpdatedBy = CurrentUser?.user_id;

                        MyContext.SaveChanges();
                    }

                }
                else
                {
                    response.Code = 400;
                    response.Message = "La liquidacion debe estar en pendiente de autorizacion";
                    return response;
                }
            }

            response.Code = 200;
            response.Message = "Ok";

            return response;

        }

        
        private async Task<ErpInvoiceCreateResponse> SendToErp(List<Liquidacion> liquidaciones)
        {
            bool hasError = false;
            try
            {
                if (liquidaciones.Any(x => !EstadoLiquidacion.Autorizada.Equals(x.Estado)))
                {
                    var result = new Result
                    {
                        HasErrors = true
                    };

                    result.Messages.Add("La liquidacion debe estar Autorizada.");

                    throw new BadRequestException(result);
                }

                ErpInvoiceCreateResponse response = new ErpInvoiceCreateResponse();
                response.InvalidRequests = new List<ErpInvoiceResponseDto>();
                response.Code = 200;

                foreach (var entity in liquidaciones)
                {
                    if (!entity.Cliente.TipoImpuestoId.HasValue || !entity.Cliente.TipoDocumentoId.HasValue || !entity.Cliente.LocalidadId.HasValue || !entity.Cliente.CondicionPagoId.HasValue)
                    {
                        var newLog = new ErpInvoiceResponseDto
                        {
                            LiquidacionId = entity.Id.ToString(),
                            Message = $"{entity.Cliente.RazonSocial} {entity.Cliente.Nombre} {entity.Cliente.Apellido} no se encuentra bien configurado.",
                            SyncDate = DateTime.Now
                        };

                        response.InvalidRequests.Add(newLog);
                        response.Code = 400;

                        await MyContext.SaveChangesAsync();
                    } 
                    else
                    {
                        var dto = ErpMilongaInvoiceService.GenerateInvoice(entity);

                        ErpInvoiceResponseDto res = await ErpMilongaInvoiceService.Sync(dto);

                        if (res.Id.HasValue && res.Id > 0)
                        {
                            entity.ErpId = res.Id.Value;
                            entity.Estado = EstadoLiquidacion.PendienteDeFacturacion;
                            entity.UpdateDate = DateTime.Now;

                            var newLog = new ErpInvoiceSyncLog
                            {
                                Id = res.Id.Value,
                                Message = "Ok",
                                SyncDate = DateTime.Now
                            };
                        }
                        else
                        {
                            var newLog = new ErpInvoiceSyncLog
                            {
                                ErpId = 0,
                                Message = res.Message,
                                SyncDate = DateTime.Now
                            };
                            response.InvalidRequests.Add(res);
                            response.Code = 400;
                        }

                        await MyContext.SaveChangesAsync();
                    }
                  
                }
                
               
                return response;
            }
            catch (Exception ex)
            {
                var newLog = new ErpInvoiceSyncLog
                {
                    ErpId = 0,
                    Message = $"{ex.Message} {ex.StackTrace}",
                    SyncDate = DateTime.Now
                };

                MyContext.ErpInvoiceSyncLogs.Add(newLog);

                await MyContext.SaveChangesAsync();

                return new ErpInvoiceCreateResponse
                {
                    Code = 500
                };
            }
        }


        public async Task<ErpInvoiceCreateResponse> SendLiquidacionesToErp(SendErpRequest updateStatusLiquidacion)
        {
            var liquidaciones = await MyContext.Liquidaciones
                .Include(x => x.DetalleLiquidacionPre)
                .Include(x => x.DetalleLiquidacionPre)
                .Include(x => x.Conceptos)
                    .ThenInclude(c => c.Concepto)
                    .ThenInclude(c => c.CodigoProducto)
                .Include(x => x.Cliente)
                    .ThenInclude(x => x.TipoDocumento)
                .Include(x => x.Cliente)
                    .ThenInclude(x => x.TipoImpuesto)
                .Include(x => x.Cliente)
                    .ThenInclude(x => x.Localidad)
                    .ThenInclude(x => x.Provincia)
                .Include(x => x.Cliente)
                    .ThenInclude(x => x.CondicionPago)
                .Include(x => x.Cliente)
                    .ThenInclude(x => x.Impuestos)
                    .ThenInclude(x => x.Impuesto)
                    .Where(u => updateStatusLiquidacion.Ids.Any(x => x == u.Id) && u.Estado == EstadoLiquidacion.Autorizada)
                    .ToListAsync();

            return await SendToErp(liquidaciones);
        }

        public async Task<int> UpdateDatosFacturacion(int id, LiquidacionDatosFacturacionRequest datosFacturacionRequest)
        {
            var liquidacion = MyContext.Liquidaciones.FirstOrDefault(u => u.Id == id);


            liquidacion.OtrosComprobantes = datosFacturacionRequest.OtrosComprobantes;
            liquidacion.NumeroFactura = datosFacturacionRequest.NumeroFactura;
            liquidacion.MontoTotalImpuestos = datosFacturacionRequest.MontoTotalImpuestos;
            liquidacion.MontoFinalFactura = datosFacturacionRequest.MontoFinalFactura;
            liquidacion.Estado = EstadoLiquidacion.Facturada;

            await MyContext.SaveChangesAsync();

            return 0;

        }

        public async Task<int> AddLiquidacionPago(LiquidacionPagoRequest liquidacionPagoRequest)
        {
            var liquidacion = MyContext.Liquidaciones.FirstOrDefault(u => u.Id == liquidacionPagoRequest.IdLiquidacion);
            var newEntity = new LiquidacionPago
            {
                IdLiquidacion = liquidacionPagoRequest.IdLiquidacion,
                ReciboId = liquidacionPagoRequest.ReciboId,
                NumeroRecibo = liquidacionPagoRequest.NumeroRecibo,
                LinkPdf = liquidacionPagoRequest.LinkPdf
            };

            liquidacion.Estado = EstadoLiquidacion.Pago;

            await MyContext.SaveChangesAsync();

            return 0;

        }

    }
}