using System;
// using System.Windows.Forms; // 这行去掉，或者不去掉也行，关键看下面

namespace ConfocalMeter
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 【关键修改】在 Application 前面加上 System.Windows.Forms.
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            // 启动主窗口
            System.Windows.Forms.Application.Run(new MainForm());
        }
    }
}