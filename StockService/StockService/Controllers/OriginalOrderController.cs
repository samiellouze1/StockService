﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StockService.Data.Enums;
using StockService.Data.IRepo;
using StockService.DTOs;
using StockService.Models;
using System.Runtime.InteropServices;
using System.Security.Claims;

namespace StockService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OriginalOrderController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IOriginalOrderRepo _originalOrderRepo;
        private readonly IMapper _mapper;
        private readonly ICreateOriginalOrderService _createooService;

        public OriginalOrderController(IMapper mapper, IOriginalOrderRepo originalOrderRepo, UserManager<User> userManager, ICreateOriginalOrderService createooService)
        {
            _originalOrderRepo = originalOrderRepo;
            _mapper = mapper;
            _userManager = userManager;
            _createooService = createooService;
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OriginalOrderReadDTO>>> GetOriginalOrders()
        {
            Console.WriteLine("--------- Getting Original Orders --------");
            var originalorders = await _originalOrderRepo.GetAllAsync();
            return Ok(_mapper.Map<IEnumerable<OriginalOrderReadDTO>>(originalorders));
        }
        [HttpGet("{id}", Name = "GetOriginalOrderById")]
        public async Task<ActionResult<OriginalOrderReadDTO>> GetOriginalOrderById(string id)
        {
            var originalorderitem = await _originalOrderRepo.GetByIdAsync(id);
            if (originalorderitem != null)
            {
                return Ok(_mapper.Map<OriginalOrderReadDTO>(originalorderitem));
            }
            else
            {
                return NotFound();
            }
        }
        //needs revision!!!
        [HttpPost("CreateOriginalOrder"), Authorize]
        public async Task<ActionResult<OriginalOrderReadDTO>> CreateOriginalOrder(OriginalOrderCreateDTO ordercreatedto)
        {
            //for visualizing progress
            Console.WriteLine("--------- Posting an Original Order --------");

            //mapping
            var originalorderModel = _mapper.Map<OriginalOrder>(ordercreatedto);

            // Get the current user
            var userId = User.FindFirst("Id").Value;
            var user = await _userManager.FindByIdAsync(userId);

            //adding the original order to db
            originalorderModel.User = user;
            originalorderModel.RemainingQuantity = ordercreatedto.OriginalQuantity;
            //Buying Order
            if (originalorderModel.OrderType == OrderTypeEnum.Buy)
            {
                if (user.Balance < originalorderModel.Price * originalorderModel.OriginalQuantity)
                {
                    return BadRequest("you don't have enough balance to do this order");
                }
                else
                {
                    var sellingOriginalOrdersList = await _createooService.GetCorrespondantSellingOrders(originalorderModel);
                    var quantityneeded = originalorderModel.OriginalQuantity;
                    foreach (var originalorder in sellingOriginalOrdersList)
                    {
                        var originalorderexecuted = true;
                        foreach (var order in originalorder.Orders.Where(o => o.OrderStatus == OrderStatusEnum.Active))
                        {
                            if (quantityneeded == 0)
                            {
                                originalorderexecuted = false;
                                break;
                            }
                            await _createooService.ExecuteOrder(order, originalorderModel.Price);
                            await _createooService.ChangeInformationOfAStockUnit(originalorder, user);
                            await _createooService.CreateExecutedOrder(originalorderModel);
                            quantityneeded -= 1;
                        }
                        if (originalorderexecuted)
                        {
                            await _createooService.ExecuteOriginalOrder(originalorder);
                        }
                    }
                    await _createooService.StoreRemainingQuantity(originalorderModel, quantityneeded);
                    for (int i = 0; i < quantityneeded; i++)
                    {
                        await _createooService.CreateInMarketOrder(originalorderModel);
                    }
                }
            }
            //selling order
            else
            {
                return BadRequest();
            }
            await _originalOrderRepo.AddAsync(originalorderModel);
            var orignalorderreaddto = _mapper.Map<OriginalOrderReadDTO>(originalorderModel);
            return CreatedAtRoute(nameof(GetOriginalOrderById), new { id = orignalorderreaddto.Id }, orignalorderreaddto);
        }

    }
}
