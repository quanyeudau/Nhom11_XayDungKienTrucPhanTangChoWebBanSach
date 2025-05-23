using ShopSach.DataAccess.Repository.IRepository;
using ShopSach.Utility;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ShopSachWeb.ViewComponents
{
    public class ShoppingCartViewComponent : ViewComponent
    {

        private readonly IUnitOfWork _unitOfWork;
        public ShoppingCartViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                if (HttpContext.Session.GetInt32(SD.SessionCart) == null)
                {
                    var cartCount = await Task.FromResult(
                        _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value).Count());
                    HttpContext.Session.SetInt32(SD.SessionCart, cartCount);
                }

                return View(HttpContext.Session.GetInt32(SD.SessionCart));
            }
            else
            {
                HttpContext.Session.Clear();
                return View(0);
            }
        }

    }
}