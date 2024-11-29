﻿using Api.Core.Dtos;
using Api.Core.Dtos.Common;
using Api.Core.Dtos.Filter;
using Api.Core.Repositories;
using Api.Core.Services.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Api.Core.Services.Implementations
{
    public class ProvinciaService : IProvinciaService
    {
        private readonly MyContext _context;
        private readonly IMapper _mapper;
        public ProvinciaService(MyContext context)
        {
            _context = context;
            _mapper = new Mapper(BootStrapper.MapperConfiguration);
        }
        public async Task<PagedListResponse<Provincia>> GetAllAsync(FilterProvincia filter)
        {
            var pageSize = filter.PageSize ?? 10;
            var currentPage = filter.CurrentPage ?? 1;

            try
            {
                var query = _context.Provincias
                    .AsNoTracking()
                    .Where(x => string.IsNullOrEmpty(filter.MultiColumnSearchText) || x.Nombre.Contains(filter.MultiColumnSearchText.TrimStart()));

                var provincias = await query.Skip(pageSize * (currentPage - 1))
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedListResponse<Provincia>
                {
                    Count = query.Count(),
                    Data = _mapper.Map<IList<Provincia>>(provincias)
                };
            }
            catch (Exception)
            {
                return new PagedListResponse<Provincia>
                {
                    Count = 0,
                    Data = new List<Provincia>()
                };
            }
        }
    }
}
