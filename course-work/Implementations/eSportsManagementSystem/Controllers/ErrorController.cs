using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace eSportsManagementSystem.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            var statusCodeResult = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            switch (statusCode)
            {
                case 404:
                    ViewBag.ErrorMessage = "Sorry, the resource you requested could not be found";
                    ViewBag.ErrorTitle = "Resource not found";
                    break;
                case 500:
                    ViewBag.ErrorMessage = "Sorry, something went wrong on the server";
                    ViewBag.ErrorTitle = "Server error";
                    break;
                default:
                    ViewBag.ErrorMessage = "Sorry, an error occurred";
                    ViewBag.ErrorTitle = "Error";
                    break;
            }

            return View("Error");
        }

        [Route("Error")]
        public IActionResult Error()
        {
            var exceptionDetails = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            ViewBag.ExceptionPath = exceptionDetails?.Path;
            ViewBag.ExceptionMessage = exceptionDetails?.Error.Message;
            ViewBag.ErrorTitle = "Error";
            
            return View();
        }
    }
}
