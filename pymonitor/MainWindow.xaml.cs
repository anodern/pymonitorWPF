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






        PerformanceCounter ramCounter = new PerformanceCounter("Process", "Working Set");
        PerformanceCounter readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec");
        PerformanceCounter writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec");


        private ObservableDataSource<Point> dataSource_cpu = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_ram = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_read = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_write = new ObservableDataSource<Point>();
        private DispatcherTimer timer = new DispatcherTimer();
        private int currentSecond = 0;
        private int interval = 100;
        private TimeSpan prevCpuTime = TimeSpan.Zero;

        private void Button_Click_2(object sender, RoutedEventArgs e) {
            RefreshProcessList();
        }
        //Process pro;

        int group = 60;//默认组距  
        int xaxis = 0;
        double yaxis_ram = 0;
        double yaxis_read = 0;
        double yaxis_write = 0;
        Queue q_cpu = new Queue();
        Queue q_ram = new Queue();
        Queue q_read = new Queue();
        Queue q_write = new Queue();

        private void timer_Tick(object sender, EventArgs e) {
            if(index >= p.Length || p[index].HasExited) {
                timer.Stop();
                RefreshProcessList();
                return;
            }
            
            //cpu
            TimeSpan curTime = p[index].TotalProcessorTime;
            double cpuValue = (curTime - prevCpuTime).TotalMilliseconds / interval /Environment.ProcessorCount * 100;
            prevCpuTime = curTime;

            //ram
            double ram, readByte, writeByte;
            try {
                ram = ramCounter.NextValue()/1048576.0;
                readByte = readBytesSec.NextValue()/1024.0;
                writeByte = writeByteSec.NextValue()/1024.0;
            }catch(InvalidOperationException) {
                timer.Stop();
                RefreshProcessList();
                return;
            }

            label_cpu.Content = string.Format("{0:F2} %", cpuValue);
            label_memory.Content = string.Format("{0:F2} MB", ram);
            label_read.Content = string.Format("{0:F2} KB/s", readByte);
            label_write.Content = string.Format("{0:F2} KB/s", writeByte);


            Point point_cpu = new Point(currentSecond, cpuValue);
            Point point_ram = new Point(currentSecond, ram);
            Point point_read = new Point(currentSecond, readByte);
            Debug.WriteLine(readByte);
            Point point_write = new Point(currentSecond, writeByte);
            dataSource_cpu.AppendAsync(Dispatcher, point_cpu);
            dataSource_ram.AppendAsync(Dispatcher, point_ram);
            dataSource_read.AppendAsync(Dispatcher, point_read);
            dataSource_write.AppendAsync(Dispatcher, point_write);


            //cpu
            if(q_cpu.Count < group) {
                q_cpu.Enqueue(cpuValue);
            } else {
                q_cpu.Dequeue();
                q_cpu.Enqueue(cpuValue);
            }

            //内存
            if(q_ram.Count < group) {
                q_ram.Enqueue(ram);
                yaxis_ram = 0;
                foreach(double c in q_ram)
                    if(c > yaxis_ram)
                        yaxis_ram = c;
            } else {
                q_ram.Dequeue();
                q_ram.Enqueue(ram);
                yaxis_ram = 0;
                foreach(double c in q_ram)
                    if(c > yaxis_ram)
                        yaxis_ram = c;
            }

            //read
            if(q_read.Count < group) {
                q_read.Enqueue(readByte);
                yaxis_read = 0;
                foreach(double c in q_read)
                    if(c > yaxis_read)
                        yaxis_read = c;
            } else {
                q_read.Dequeue();
                q_read.Enqueue(readByte);
                yaxis_read = 0;
                foreach(double c in q_read)
                    if(c > yaxis_read)
                        yaxis_read = c;
            }

            //write
            if(q_write.Count < group) {
                q_write.Enqueue(writeByte);
                yaxis_write = 0;
                foreach(double c in q_write)
                    if(c > yaxis_write)
                        yaxis_write = c;
            } else {
                q_write.Dequeue();
                q_write.Enqueue(writeByte);
                yaxis_write = 0;
                foreach(double c in q_write)
                    if(c > yaxis_write)
                        yaxis_write = c;
            }

            if(currentSecond - group > 0) xaxis = currentSecond - group;
            else xaxis = 0;

            plotter_cpu.Viewport.Visible = new Rect(xaxis, 0, group, 100);
            plotter_ram.Viewport.Visible = new Rect(xaxis, 0, group, yaxis_ram*1.1);
            plotter_read.Viewport.Visible = new Rect(xaxis, 0, group, yaxis_read*1.1);
            plotter_write.Viewport.Visible = new Rect(xaxis, 0, group, yaxis_write*1.1);
            currentSecond++;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            timer.Stop();
            index = combo_process.SelectedIndex;
            string pname = "python";
            if(index>0) {
                pname += "#"+index;
            }

            ramCounter.InstanceName = pname;
            readBytesSec.InstanceName = pname;
            writeByteSec.InstanceName = pname;

            timer.Start();
        }

        Process[] p;
        int index;
        private void Window_Loaded(object sender, RoutedEventArgs e) {
            plotter_cpu.AddLineGraph(dataSource_cpu, Colors.Red, 1.5, " ");
            plotter_cpu.LegendVisible = true;
            plotter_ram.AddLineGraph(dataSource_ram, Colors.Red, 1.5, " ");
            plotter_ram.LegendVisible = true;
            plotter_read.AddLineGraph(dataSource_read, Colors.Red, 1.5, " ");
            plotter_read.LegendVisible = true;
            plotter_write.AddLineGraph(dataSource_write, Colors.Red, 1.5, " ");
            plotter_write.LegendVisible = true;



            plotter_cpu.Viewport.FitToView();
            plotter_ram.Viewport.FitToView();
            plotter_read.Viewport.FitToView();
            plotter_write.Viewport.FitToView();

            RefreshProcessList();

            timer.Interval = TimeSpan.FromMilliseconds(interval);
            timer.Tick += timer_Tick;
            timer.IsEnabled = false;

        }

        private void RefreshProcessList() {
            combo_process.Items.Clear();
            p = Process.GetProcessesByName("python");
            for(int i = 0; i<p.Length; i++) {
                combo_process.Items.Add(p[i].ProcessName + (i>0 ? "#"+i : ""));
            }
        }



        /*
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
        */
    }
}
