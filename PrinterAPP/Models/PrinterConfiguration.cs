using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PrinterAPP.Models;
public class PrinterConfiguration
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5221";
    public string PrinterName { get; set; } = "";
    public bool AutoPrint { get; set; } = true;
    public int PrintCopies { get; set; } = 1;
    public string RestaurantName { get; set; } = "Your Restaurant";
    public string KitchenLocation { get; set; } = "Main Kitchen";
    public int PaperWidth { get; set; } = 80;
    public bool IsServiceRunning { get; set; } = false;
}