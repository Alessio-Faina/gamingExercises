using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Data.Common;
using System.Net.NetworkInformation;


namespace FallingSand
{
    public static class GlobalConsts
    {
        public const int WIDTH = 640;
        public const int HEIGHT = 480;
    }

    public static class Utilities
    {
        public static int ConvertColorToInt(Color color)
        {
            int value = (color.A << 24) | (color.B << 16) | (color.G << 8) | color.R;
            return value;
        }
    }

    internal class SandGrain(int colour)
    {
        int g_colourData = colour;
        int g_fallingSpeed = 1;

        public int Colour
        {
            get { return g_colourData; }
        }

        public int Speed
        {
            get { return g_fallingSpeed; }
        }
    }

    internal class SandWall
    {
        private SandGrain[,] array = new SandGrain[GlobalConsts.WIDTH, GlobalConsts.HEIGHT];

        public void AddGrain(int posX, int posY, int colour)
        {
            if (posX > 0 && posY > 0 && posX <= GlobalConsts.WIDTH && posY <= GlobalConsts.HEIGHT)
            {
                if (array[posX, posY] == null)
                {
                    array[posX, posY] = new SandGrain(colour);
                }
            }
        }

        public SandGrain this[int x, int y]
        {
            get
            {
                return array[x, y];
            }
            set
            {
                array[x, y] = value;
            }
        }
    }

    internal class Game
    {
        static Window mainWindow;
        static Image? gameCanvas;
        static WriteableBitmap writeableBitmap;
        static bool Running = false;
        static bool isLeftMousePressed = false;

        [STAThread]
        static void Main(string[] args)
        {
            mainWindow = new Window()
            {
                Width = 700,
                Height = 560
            };

            gameCanvas = new Image()
            {
                Width = GlobalConsts.WIDTH,
                Height = GlobalConsts.HEIGHT
            };
            mainWindow.Content = gameCanvas;
            mainWindow.Show();

            RenderOptions.SetBitmapScalingMode(gameCanvas, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(gameCanvas, EdgeMode.Aliased);


            writeableBitmap = new WriteableBitmap(
                (int)gameCanvas.Width,
                (int)gameCanvas.Height,
                96,
                96,
                PixelFormats.Bgr32,
                null);
            gameCanvas.Source = writeableBitmap;
            gameCanvas.Stretch = Stretch.None;
            gameCanvas.HorizontalAlignment = HorizontalAlignment.Left;
            gameCanvas.VerticalAlignment = VerticalAlignment.Top;
            gameCanvas.MouseLeftButtonDown += GameCanvas_MouseLeftButtonDown;
            gameCanvas.MouseLeftButtonUp += GameCanvas_MouseLeftButtonUp;
            mainWindow.Closing += 
                (sender, args) => { Running = false; };

            var gameThread = new Thread(GameLoop);
            gameThread.SetApartmentState(ApartmentState.STA);
            gameThread.Start();

            var app = new Application();
            app.Run();
        }

        static private void GameCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isLeftMousePressed = true;
        }

        static private void GameCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isLeftMousePressed = false;
        }

        static void HandleEvents(ref SandWall wall, ref int colour)
        {
            if (isLeftMousePressed)
            {
                var pos = Mouse.GetPosition(gameCanvas);
                int minX = Math.Max(0, (int)pos.X - 20);
                int maxX = Math.Min(GlobalConsts.WIDTH, (int)pos.X + 20);
                int minY = Math.Max(0, (int)pos.Y - 20);
                int maxY = Math.Min(GlobalConsts.HEIGHT, (int)pos.Y + 20);

                for (int x = minX; x < maxX; x++)
                {
                    for (int y = minY; y < maxY; y++)
                    {
                        wall.AddGrain(x, y, colour);
                    }
                }
            }
        }

        static void SceneUpdate(ref SandWall wall)
        {
            int Black = Utilities.ConvertColorToInt(Colors.Black);
            try
            {
                writeableBitmap.Lock();
                unsafe
                {
                    for (int y = 0; y < GlobalConsts.HEIGHT; y++)
                    {
                        for (int x = 0; x < GlobalConsts.WIDTH; x++)
                        {
                            IntPtr pBackBuffer = writeableBitmap.BackBuffer;
                            pBackBuffer += y * writeableBitmap.BackBufferStride;
                            pBackBuffer += x * 4;

                            if (wall[x,y] != null)
                            {
                                *((int*)pBackBuffer) = wall[x, y].Colour;
                            }
                            else
                            {
                                *((int*)pBackBuffer) = Black;
                            }
                        }
                    }
                    
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, GlobalConsts.WIDTH, GlobalConsts.HEIGHT));
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                writeableBitmap.Unlock();
            }
        }

        static void PhysicsEngine(ref SandWall wall)
        {
            Random rnd = new Random();
            for (int y = GlobalConsts.HEIGHT - 2; y > 0; y--)
            {
                for (int x = GlobalConsts.WIDTH - 1; x > 0; x--)
                {
                    int speed = wall[x, y].Speed;
                    if (wall[x,y] != null)
                    {
                        if (wall[x, y+1] == null)
                        {
                            wall[x, y + 1] = wall[x, y];
                            wall[x, y] = null;
                        }
                        else
                        {
                            var leftOrRight = rnd.Next(9);
                            if (x < GlobalConsts.WIDTH - 1 && leftOrRight >= 5 && wall[x + 1, y + 1] == null) // go right
                            {
                                wall[x + 1, y + 1] = wall[x, y];
                                wall[x, y] = null;
                            } else if (x > 1 && leftOrRight <5 && wall[x -1, y + 1] == null)
                            {
                                wall[x - 1, y + 1] = wall[x, y];
                                wall[x, y] = null;
                            }

                        }
                    }
                }
            }
        }

        [STAThread]
        static void GameLoop()
        {
            SandWall wall = new();
            int colourNow = Utilities.ConvertColorToInt(Colors.White);

            var targetDelta = TimeSpan.FromMilliseconds(16);
            Running = true;
            while (Running)
            {
                long startFrameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                // Handle events
                mainWindow.Dispatcher.Invoke(new Action(() => {HandleEvents(ref wall, ref colourNow); }));

                // Physics update
                PhysicsEngine(ref wall);

                // Scene Render
                writeableBitmap.Dispatcher.Invoke(new Action(() => {SceneUpdate(ref wall); }));

                long endFrameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                var frameDelta = TimeSpan.FromMilliseconds(endFrameTime - startFrameTime);
                if (frameDelta < targetDelta)
                {
                    System.Threading.Thread.Sleep(targetDelta);
                }
                colourNow += 255;
                //if (colourNow > Utilities.ConvertColorToInt(Colors.White)) {
                //    colourNow = 16;
               // }
            }
        }
    }
}
