using ShopSach.DataAccess.Repository.IRepository;
using ShopSach.Models;
using ShopSach.Models.ViewModels;
using ShopSach.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

using System.Security.Claims;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace ShopSachWeb.Areas.Customer.Controllers
{
	[Area("customer")]
	[Authorize]
	public class CartController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        [BindProperty]
		public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork, IEmailSender emailSender, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        public IActionResult Index()
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            IEnumerable<ProductImage> productImages = _unitOfWork.ProductImage.GetAll();

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Product.ProductImages = productImages.Where(u => u.ProductId == cart.Product.Id).ToList();
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

		public IActionResult Summary()
        {
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;

            IEnumerable<ProductImage> productImages = _unitOfWork.ProductImage.GetAll();

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Product.ProductImages = productImages.Where(u => u.ProductId == cart.Product.Id).ToList();
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
		public IActionResult SummaryPOST(string paymentMethod = "vnpay")
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product");

            ShoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserId = userId;

            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;

            _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWork.Save();

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
                    Price = cart.Price,
                    Count = cart.Count
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
            }
            _unitOfWork.Save();

            // Log VNPay payment parameters for debugging
            Debug.WriteLine("Starting VNPay payment process");
            Debug.WriteLine($"Order ID: {ShoppingCartVM.OrderHeader.Id}");
            Debug.WriteLine($"Order Total: {ShoppingCartVM.OrderHeader.OrderTotal}");

            // VNPay payment setup
            string url = _configuration["VNPay:PaymentUrl"];
            string returnUrl = _configuration["VNPay:ReturnUrl"];
            string tmnCode = _configuration["VNPay:TmnCode"];
            string hashSecret = _configuration["VNPay:HashSecret"];

            Debug.WriteLine($"VNPay URL: {url}");
            Debug.WriteLine($"VNPay Return URL: {returnUrl}");
            Debug.WriteLine($"VNPay TMN Code: {tmnCode}");
            Debug.WriteLine($"VNPay Hash Secret: {hashSecret}");

            PayLib pay = new PayLib();
            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", tmnCode);
            pay.AddRequestData("vnp_Amount", ((long)(ShoppingCartVM.OrderHeader.OrderTotal * 100)).ToString());
            pay.AddRequestData("vnp_BankCode", "NCB");
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", Util.GetIpAddress(HttpContext));
            pay.AddRequestData("vnp_Locale", "vn");
            pay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn hàng #{ShoppingCartVM.OrderHeader.Id}");
            pay.AddRequestData("vnp_OrderType", "other");
            pay.AddRequestData("vnp_ReturnUrl", returnUrl);
            pay.AddRequestData("vnp_TxnRef", ShoppingCartVM.OrderHeader.Id.ToString());

            string paymentUrl = pay.CreateRequestUrl(url, hashSecret);
            Debug.WriteLine($"Generated VNPay URL: {paymentUrl}");
            
            return Redirect(paymentUrl);
        }

        public IActionResult VNPayReturn()
        {
            try
            {
                Debug.WriteLine("Processing VNPay return");
                PayLib pay = new PayLib();
                var response = HttpContext.Request.Query;

                foreach (var (key, value) in response)
                {
                    if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    {
                        pay.AddResponseData(key, value.ToString());
                        Debug.WriteLine($"VNPay response param: {key}={value}");
                    }
                }

                // Get important data from response
                var vnPayTranId = pay.GetResponseData("vnp_TransactionNo");
                var vnpResponseCode = pay.GetResponseData("vnp_ResponseCode");
                string vnp_SecureHash = Request.Query["vnp_SecureHash"];
                
                Debug.WriteLine($"VNPay Transaction ID: {vnPayTranId}");
                Debug.WriteLine($"VNPay Response Code: {vnpResponseCode}");
                
                string hashSecret = _configuration["VNPay:HashSecret"];
                Debug.WriteLine($"Using Hash Secret: {hashSecret}");
                
                bool checkSignature = pay.ValidateSignature(vnp_SecureHash, hashSecret);
                Debug.WriteLine($"Signature validation result: {checkSignature}");

                if (checkSignature)
                {
                    if (vnpResponseCode == "00")
                    {
                        Debug.WriteLine("VNPay payment successful");
                        // Get the latest order created for this user
                        var claimsIdentity = (ClaimsIdentity)User.Identity;
                        var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                        
                        var orderHeader = _unitOfWork.OrderHeader.GetAll(u => u.ApplicationUserId == userId)
                            .OrderByDescending(o => o.OrderDate)
                            .FirstOrDefault();

                        if (orderHeader != null)
                        {
                            Debug.WriteLine($"Found order: {orderHeader.Id}");
                            orderHeader.OrderStatus = SD.StatusApproved;
                            orderHeader.PaymentStatus = SD.PaymentStatusApproved;
                            orderHeader.PaymentDate = DateTime.Now;
                            orderHeader.TransactionId = vnPayTranId;
                            _unitOfWork.OrderHeader.Update(orderHeader);
                            _unitOfWork.Save();

                            return RedirectToAction(nameof(OrderConfirmation), new { id = orderHeader.Id });
                        }
                        else
                        {
                            Debug.WriteLine("No order found for user");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"VNPay transaction failed with code: {vnpResponseCode}");
                        TempData["error"] = "Giao dịch không thành công. Mã lỗi: " + vnpResponseCode;
                    }
                }
                else
                {
                    Debug.WriteLine("Invalid signature from VNPay");
                    TempData["error"] = "Chữ ký không hợp lệ. Giao dịch bị từ chối.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in VNPay processing: {ex.Message}");
                TempData["error"] = "Đã xảy ra lỗi trong quá trình xử lý thanh toán: " + ex.Message;
            }
            return RedirectToAction("Index", "Home", new { area = "Customer" });
        }

        public IActionResult OrderConfirmation(int id)
		{
			OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");
			if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
			{
				//this is an order by customer
				_unitOfWork.ShoppingCart.RemoveRange(_unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId));
				_unitOfWork.Save();
			}

			return View(id);
		}

		public IActionResult Plus(int cartId)
		{
			var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
			cartFromDb.Count += 1;
			_unitOfWork.ShoppingCart.Update(cartFromDb);
			_unitOfWork.Save();
			return RedirectToAction(nameof(Index));
		}

		public IActionResult Minus(int cartId)
		{
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            if (cartFromDb.Count <= 1)
			{
                //remove that from cart
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
                    .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
			}
			else
			{
				cartFromDb.Count -= 1;
				_unitOfWork.ShoppingCart.Update(cartFromDb);
			}

			_unitOfWork.Save();
			return RedirectToAction(nameof(Index));
		}

		public IActionResult Remove(int cartId)
		{
			var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
                    .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            _unitOfWork.Save();
			return RedirectToAction(nameof(Index));
		}

        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }
}