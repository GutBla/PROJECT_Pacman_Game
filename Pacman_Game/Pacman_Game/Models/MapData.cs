using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pacman_Game.Models
{
    public class MapData
    {
        public string MapName { get; set; } = "default";
        public int[,] GameMap { get; set; } = new int[0, 0];
        public string[,] MapTextures { get; set; } = new string[0, 0];
        public string[,] Elements { get; set; } = new string[0, 0];
    }
}