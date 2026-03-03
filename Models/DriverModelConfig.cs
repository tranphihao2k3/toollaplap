using System.Collections.Generic;

namespace LapLapAutoTool.Models
{
    public class DriverModelConfig
    {
        public string Model { get; set; } = string.Empty;
        public List<string> ModelMatch { get; set; } = new();
        public List<DriverItem> Drivers { get; set; } = new();
    }
}
