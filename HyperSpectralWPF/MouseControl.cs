using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HyperSpectralWPF
{
    /// <summary>
    /// 
    /// </summary>
    class MouseControl
    {
        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <see>See MSDN documentation for further information.</see>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="data"></param>
        /// <param name="extraInfo"></param>
        [DllImport("user32.dll")]
        static extern void mouse_event(MouseEventFlag flags, int dx, int dy, uint data, UIntPtr extraInfo);

        /// <summary>
        /// 
        /// </summary>
        [Flags]
        enum MouseEventFlag : uint
        {
            Move        = 0x0001,
            LeftDown    = 0x0002,
            LeftUp      = 0x0004,
            RightDown   = 0x0008,
            RightUp     = 0x0010,
            MiddleDown  = 0x0020,
            MiddleUp    = 0x0040,
            XDown       = 0x0080,
            XUp         = 0x0100,
            Wheel       = 0x0800,
            VirtualDesk = 0x4000,
            Absolute    = 0x8000
        }

        /// <summary>
        /// Struct representing a point.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static void MouseLeftDown()
        {
            mouse_event(MouseEventFlag.LeftDown, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void MouseLeftUp()
        {
            mouse_event(MouseEventFlag.LeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void DoMouseClick()
        {
            mouse_event(MouseEventFlag.LeftDown | MouseEventFlag.LeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);

            return lpPoint;
        }
    }
}
