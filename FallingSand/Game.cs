using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Data.Common;
using System.Net.NetworkInformation;
using System.Windows.Media.Media3D;


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
            uint value = (uint)((color.B << 16) | (color.G << 8) | color.R);
            return (int)value;
        }
    }

    internal class SandGrain(int colour)
    {
        const int maxFallingSpeed = 25;
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
        public void Accelerate()
        {
            g_fallingSpeed = Math.Min(g_fallingSpeed + 1, maxFallingSpeed);
        }

        public void Brake()
        {
            g_fallingSpeed = Math.Min(g_fallingSpeed / 2, 0);
            if (g_fallingSpeed == 1) g_fallingSpeed = 0;
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

        public void DeleteGrain(int posX, int posY)
        {
            if (posX > 0 && posY > 0 && posX <= GlobalConsts.WIDTH && posY <= GlobalConsts.HEIGHT)
            {
                if (array[posX, posY]  != null)
                {
                    array[posX, posY] = null;
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
        static Window debugWindow;
        static Image? gameCanvas;
        static WriteableBitmap writeableBitmap;
        static bool Running = false;
        static bool isLeftMousePressed = false;
        static bool isRightMousePressed = false;
        static TextBlock debugText;

        [STAThread]
        static void Main(string[] args)
        {
            mainWindow = new Window()
            {
                Width = 700,
                Height = 560,
                Left = 300
            };

            gameCanvas = new Image()
            {
                Width = GlobalConsts.WIDTH,
                Height = GlobalConsts.HEIGHT,
            };
            mainWindow.Content = gameCanvas;
            mainWindow.Show();

            debugWindow = new Window()
            {
                Width = 100,
                Height = 160,
                Left = mainWindow.Left - 100
            };
            debugText = new TextBlock();
            debugWindow.Content = debugText;
            debugWindow.Show();

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
            gameCanvas.MouseRightButtonDown += GameCanvas_MouseRightButtonDown;
            gameCanvas.MouseRightButtonUp += GameCanvas_MouseRightButtonUp;
            mainWindow.Closing += 
                (sender, args) => { 
                    Running = false;
                    debugWindow.Close();
                };

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

        static private void GameCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            isRightMousePressed = true;
        }

        static private void GameCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            isRightMousePressed = false;
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
            else if (isRightMousePressed)
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
                        wall.DeleteGrain(x, y);
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
        public static Color Rainbow(float progress)
        {
            float div = (Math.Abs(progress % 1) * 6);
            int ascending = (int)((div % 1) * 255);
            int descending = 255 - ascending;

            switch ((int)div)
            {
                case 0:
                    return Color.FromArgb(255, 255, (byte)ascending, 0);
                case 1:
                    return Color.FromArgb(255, (byte)descending, 255, 0);
                case 2:
                    return Color.FromArgb(255, 0, 255, (byte)ascending);
                case 3:
                    return Color.FromArgb(255, 0, (byte)descending, 255);
                case 4:
                    return Color.FromArgb(255, (byte)ascending, 0, 255);
                default: // case 5:
                    return Color.FromArgb(255, 255, 0, (byte)descending);
            }
        }

        static int FindNextAvailableSpot(ref SandWall wall, int x, int y, int speed)
        {
            if ((x < 0) || (x >= GlobalConsts.WIDTH))
            {
                return -1;
            }
            int maxy = Math.Min(y + speed, GlobalConsts.HEIGHT - 1);
            maxy = Math.Max(maxy, 0);
            int result = -1;
            for (int i = y + 1; i <= maxy; i++)
            {
                if (wall[x, i] == null) result = i;
                else break;
            }
            return result;
        }

        static int preL = 0;
        static int preR = 0;
        static int L = 0;
        static int R = 0;

        static void PhysicsEngine(ref SandWall wall)
        {
            Random rnd = new Random();
            int direction = 0;
            for (int y = GlobalConsts.HEIGHT - 2; y > 0; y--)
            {
                //for (int x = GlobalConsts.WIDTH - 1; x > 0; x--)
                for (int x = 0; x < GlobalConsts.WIDTH - 1; x++)
                {
                    if (wall[x,y] != null)
                    {
                        int speed = wall[x, y].Speed;
                        int nextVerticalSpot = FindNextAvailableSpot(ref wall, x, y, speed);
                        if (nextVerticalSpot != -1)
                        {
                            wall[x, y].Accelerate();
                            wall[x, nextVerticalSpot] = wall[x, y];
                            wall[x, y] = null;
                        }
                        else
                        {
                            //wall[x, y].Brake();

                            int left = FindNextAvailableSpot(ref wall, x - 1, y, speed);
                            int right = FindNextAvailableSpot(ref wall, x + 1, y, speed);

                            if (left != -1 && right != -1)
                            {
                                if (rnd.Next(0, 2) == 0)
                                {
                                    right = -1;
                                }
                                else
                                {
                                    left = -1;
                                }
                            }

                            if ((left != -1) && (right == -1))
                            {
                                if (x < GlobalConsts.WIDTH - 1)
                                {
                                    wall[x - 1, left] = wall[x, y];
                                    wall[x, y] = null;
                                }
                            } else if ((left == -1) && (right != -1))
                            {
                                if (x > 1)
                                {
                                    wall[x + 1, right] = wall[x, y];
                                    wall[x, y] = null;
                                }
                            }

                            /*
                            if (right != -1 && x < GlobalConsts.WIDTH - 1 && direction == 1 && wall[x + 1, right] == null) // go right
                            {
                                wall[x + 1, right] = wall[x, y];
                                wall[x, y] = null;
                                R++;
                            }
                            else if (left != -1 && x > 1 && direction == 0 && wall[x - 1, left] == null)
                            {
                                wall[x - 1, left] = wall[x, y];
                                wall[x, y] = null;
                                L++;
                            }
                            */
                        }
                    }
                }
            }
            /*
            debugText.Dispatcher.Invoke(new Action(() =>
            {
                debugText.Text = "preL: " + preL.ToString() + "\n";
                debugText.Text += "preR: " + preR.ToString() + "\n";
                debugText.Text += "L: " + L.ToString() + "\n";
                debugText.Text += "R: " + R.ToString() + "\n";
            }
            ));
            */
        }

        [STAThread]
        static void GameLoop()
        {
            double[] fps = new double[10];
            int fpsIndex = 0;
            SandWall wall = new();
            float colourIndex = 0;

            var targetDelta = TimeSpan.FromMilliseconds(16);
            Running = true;
            while (Running)
            {
                int colourNow = Utilities.ConvertColorToInt(Rainbow(colourIndex));
                long startFrameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                // Handle events
                mainWindow.Dispatcher.Invoke(new Action(() => { HandleEvents(ref wall, ref colourNow); }));

                // Physics update
                PhysicsEngine(ref wall);

                // Scene Render
                writeableBitmap.Dispatcher.Invoke(new Action(() => { SceneUpdate(ref wall); }));

                long endFrameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                var frameDelta = TimeSpan.FromMilliseconds(endFrameTime - startFrameTime);
                if (frameDelta < targetDelta)
                {
                    System.Threading.Thread.Sleep(targetDelta);
                }
                
                colourIndex += (float)0.01;
                if (colourIndex >= 100) colourIndex = 0;

                fps[fpsIndex++ % 10] = ((1 / frameDelta.TotalMilliseconds) * 1000);
                double avg = 0;
                for (int i = 0; i < 10; i++)
                {
                    avg += fps[i];
                }
                avg /= 10;
                debugText.Dispatcher.Invoke(new Action(() =>
                { debugText.Text = "FPS " + avg.ToString() + "\n"; }
                ));
            }
        }
    }
}
