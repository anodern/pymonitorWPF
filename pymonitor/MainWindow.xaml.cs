using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace pymonitor {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow:Window {
        public MainWindow() {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            Process p = Process.GetProcessById(27156);
            string pn = p.ProcessName;
            var readOpSec = new PerformanceCounter("Process", "IO Read Operations/sec", pn);
            var writeOpSec = new PerformanceCounter("Process", "IO Write Operations/sec", pn);
            var dataOpSec = new PerformanceCounter("Process", "IO Data Operations/sec", pn);
            var readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec", pn);
            var writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec", pn);
            var dataBytesSec = new PerformanceCounter("Process", "IO Data Bytes/sec", pn);

            var counters = new List<PerformanceCounter> {
                readOpSec,
                writeOpSec,
                dataOpSec,
                readBytesSec,
                writeByteSec,
                dataBytesSec
            };


            foreach(PerformanceCounter counter in counters) {
                float rawValue = counter.NextValue();
                Debug.WriteLine(rawValue);
            }
        }

        private static void PerformanceCounterFun(string CategoryName, string InstanceName, string CounterName) {
            PerformanceCounter pc = new PerformanceCounter(CategoryName, CounterName, InstanceName);
            while(true) {
                Thread.Sleep(200); // wait for 1 second 
                float cpuLoad = pc.NextValue();
                Debug.WriteLine("CPU load = " + cpuLoad + " %.");
            }
        }

        public static void GetCategoryNameList() {
            PerformanceCounterCategory[] myCat2;
            myCat2 = PerformanceCounterCategory.GetCategories();
            for(int i = 0; i < myCat2.Length; i++) {
                Debug.WriteLine(myCat2[i].CategoryName.ToString());
            }
        }
        public static void GetInstanceNameListANDCounterNameList(string CategoryName) {
            string[] instanceNames;
            ArrayList counters = new ArrayList();
            PerformanceCounterCategory mycat = new PerformanceCounterCategory(CategoryName);
            try {
                instanceNames = mycat.GetInstanceNames();
                if(instanceNames.Length == 0) {
                    counters.AddRange(mycat.GetCounters());
                } else {
                    for(int i = 0; i < instanceNames.Length; i++) {
                        counters.AddRange(mycat.GetCounters(instanceNames[i]));
                    }
                }
                for(int i = 0; i < instanceNames.Length; i++) {
                    Debug.WriteLine(instanceNames[i]);
                }
                Debug.WriteLine("******************************");
                foreach(PerformanceCounter counter in counters) {
                    Debug.WriteLine(counter.CounterName);
                }
            } catch(Exception) {
                Debug.WriteLine("Unable to list the counters for this category");
            }
        }








        //PerformanceCounter readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec");
        //PerformanceCounter writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec");



        private ObservableDataSource<Point> dataSource_cpu = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_ram = new ObservableDataSource<Point>();
        private DispatcherTimer timer = new DispatcherTimer();
        private int currentSecond = 0;

        private void Button_Click_2(object sender, RoutedEventArgs e) {
            plotter_cpu.AddLineGraph(dataSource_cpu, Colors.Black, 1);
            plotter_cpu.LegendVisible = true;
            plotter_ram.AddLineGraph(dataSource_ram, Colors.Black, 1);
            plotter_ram.LegendVisible = true;

            timer.Interval = TimeSpan.FromMilliseconds(interval);
            timer.Tick += timer_Tick;
            timer.IsEnabled = true;


            //readBytesSec.InstanceName = "python";
            //writeByteSec.InstanceName = "python";
            //plotter.Viewport.FitToView();

            Process[] p = Process.GetProcessesByName("python");
            if(p.Length<1) return;
            pro = p[1];
        }
        Process pro;

        int xaxis = 0;
        int yaxis = 100;
        int group = 60;//默认组距  
        Queue q_cpu = new Queue();
        Queue q_ram = new Queue();

        private void timer_Tick(object sender, EventArgs e) {
            if(pro.HasExited) {
                timer.Stop();
                return;
            }
            Debug.WriteLine(currentSecond);
            TimeSpan curTime = pro.TotalProcessorTime;
            double cpuValue = (curTime - prevCpuTime).TotalMilliseconds / interval /Environment.ProcessorCount * 100;
            prevCpuTime = curTime;

            double ram = pro.WorkingSet64/1048576.0;
            string ramStr = string.Format("{0:F2} MB", ram);

            double x_cpu = currentSecond;
            double y_cpu = cpuValue;
            Point point_cpu = new Point(currentSecond, y_cpu);
            Point point_ram = new Point(currentSecond, ram);
            dataSource_cpu.AppendAsync(Dispatcher, point_cpu);
            dataSource_ram.AppendAsync(Dispatcher, point_ram);


            if(q_cpu.Count < group) {
                q_cpu.Enqueue((int)y_cpu);//入队  
                /*yaxis  = 0;
                foreach(int c in q)
                    if(c > yaxis)
                        yaxis = c;*/
            } else {
                q_cpu.Dequeue();//出队  
                q_cpu.Enqueue((int)y_cpu);//入队  
                /*yaxis = 0;
                foreach(int c in q)
                    if(c > yaxis)
                        yaxis = c;*/
            }

            if(q_ram.Count < group) {
                q_ram.Enqueue((int)ram);
            } else {
                q_ram.Dequeue();//出队  
                q_ram.Enqueue((int)ram);
            }

            if(currentSecond - group > 0) xaxis = currentSecond - group;
            else xaxis = 0;

            plotter_cpu.Viewport.Visible = new Rect(xaxis, 0, group, yaxis);
            plotter_ram.Viewport.Visible = new Rect(xaxis, 0, group, yaxis);
            currentSecond++;
        }








        int interval = 100;
        TimeSpan prevCpuTime = TimeSpan.Zero;


        Thread threadGetData;
        private void Button_Click_1(object sender, RoutedEventArgs e) {
            //ClearLabel();
            //threadGetData = new Thread(GetDataThread) {
            //    IsBackground = true
            //};
            //threadGetData.Start();
        }

        private void ClearLabel() {
            label_cpu.Content = string.Empty;
            label_memory.Content = string.Empty;
            label_read.Content = string.Empty;
            label_write.Content = string.Empty;
        }

        private void GetDataThread() {
            Process[] p = Process.GetProcessesByName("python");

            
            


            string pn = p[1].ProcessName;
            //PerformanceCounter cpuCounter = new PerformanceCounter("Process", "% Processor Time", pn);
            //PerformanceCounter ramCounter = new PerformanceCounter("Process", "Working Set", pn);
            PerformanceCounter readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec", pn);
            PerformanceCounter writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec", pn);

            //Process pro = p[1];
            string cpuStr, ramStr, readStr, writeStr;
            while(true) {

                TimeSpan curTime = pro.TotalProcessorTime;
                double cpuValue = (curTime - prevCpuTime).TotalMilliseconds / interval /Environment.ProcessorCount * 100;
                prevCpuTime = curTime;


                cpuStr = string.Format("{0:F2} %", cpuValue);
                ramStr = string.Format("{0:F2} MB", pro.WorkingSet64/1048576.0);

                try {
                    //cpuStr = string.Format("{0:F2} %", cpuCounter.NextValue());
                    //ramStr = string.Format("{0:F2} MB", ramCounter.NextValue()/1024/1024);
                    readStr = string.Format("{0:F2} KB/s", readBytesSec.NextValue()/1024);
                    writeStr = string.Format("{0:F2} KB/s", writeByteSec.NextValue()/1024);
                }catch(InvalidOperationException ioe) {
                    break;
                }

                try {
                    ThreadPool.QueueUserWorkItem(o => {
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, new Action(() => {
                            label_cpu.Content = cpuStr;
                            label_memory.Content = ramStr;
                            label_read.Content = readStr;
                            label_write.Content = writeStr;
                        }));
                    });
                }catch(Exception e) {
                    break;
                }

                Thread.Sleep(interval);
            }
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, new Action(() => {
                ClearLabel();
            }));
        }

    }
}
