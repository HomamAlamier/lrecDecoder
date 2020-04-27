using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace lrecDecoder
{
    class Program
    {


        static void Main(string[] args)
        {
            Form frm = new Form();
            frm.BackgroundImageLayout = ImageLayout.Center;
            Button x1 = new Button()
            {
                Text = "Play",
                Size = new Size(60, 20),
                Location = new Point(10, 10)
            };
            frm.Controls.Add(x1);
            Button x2 = new Button()
            {
                Text = "Pause",
                Size = new Size(60, 20),
                Location = new Point(80, 10)
            };
            frm.Controls.Add(x2);
            Button x3 = new Button()
            {
                Text = "Stop",
                Size = new Size(60, 20),
                Location = new Point(150, 10)
            };
            frm.Controls.Add(x3);
            new Thread(() =>
            {
                frm.Show();
                while (true)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
            }).Start();

            Lrec_FileReader l = new Lrec_FileReader("1.lrec", null);

            int vr = 0;


            //l.ImageProcess += (e) =>
            //{
            //    if (e != null)
            //    {
            //        int ind = 0;
            //        int st = (int)e[0].time / e.Length;
            //        new Thread(() =>
            //        {
            //            while (ind < e.Length)
            //            {
            //                frm.Invoke(new Action(() =>
            //                {
            //                    //if (frm.BackgroundImage == null) frm.BackgroundImage = new Bitmap(frm.Width, frm.Height);
            //                    frm.CreateGraphics().DrawImage(e[ind].image, e[ind].rect);
            //                }));
            //                ind++;
            //                Thread.Sleep(st);
            //            }
            //        }).Start();
            //        Console.WriteLine("Starting timer " + e[0].time / e.Length + "ms / " + e[0].time + "ms");

            //    }
            //};
            new Thread(() =>
            {
                int fram = 1;
                var stamps = l.TimeStamps;
                int millis = 0;
                int old = 0;
                int ind = 0;
                ImageFrame[] ff = null;
                Graphics g = null;
                while (true)
                {
                    if (l.Playing)
                    {
                        if (g == null) g = frm.CreateGraphics();
                        if (millis >= stamps[fram])
                        {
                            ff = l.GetBufferedNextFrame();
                            ind = 0;
                            if (ff != null)
                            {
                                //g.Clip = new Region(frm.ClientRectangle);
                                g.DrawImage(ff[0].image, ff[0].rect);
                                old = millis;
                            }
                            fram++;
                            frm.Invoke(new Action(() => { frm.Text = millis / 1000 + "s | " + DateTime.Now.Second.ToString() + " | " + stamps[fram]; }));
                        }
                        if (ff != null && millis - old >= (int)ff[0].time / ff.Length && ind < ff.Length)
                        {
                            //g.Clip = new Region(frm.ClientRectangle);
                            g.DrawImage(ff[ind].image, ff[ind].rect);
                            old = millis;
                            ind++;
                        }

                        millis++;

                    }
                    Thread.Sleep(1);
                }

            }).Start();


            x1.Click += delegate
            {
                l.Start();
            };

            x2.Click += delegate
            {
                l.Pause();
            };
            x3.Click += delegate
            {
                l.Stop();
            };

            frm.Click += delegate
            {
                l.GetBufferedNextFrame();
            };
            Console.ReadKey();

        }

    }
}
