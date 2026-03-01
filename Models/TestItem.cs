using System;
using System.Windows.Input;

namespace LapLapAutoTool.Models
{
    public class TestItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconData { get; set; }
        public ICommand Command { get; set; }
        public string Status { get; set; } = "Sẵn sàng";
        public string AccentColor { get; set; } = "#3B82F6"; // Default blue
    }
}
