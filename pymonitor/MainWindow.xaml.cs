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
        Thread threadGetData;
        private void Button_Click_1(object sender, RoutedEventArgs e) {
            ClearLabel();
            threadGetData = new Thread(GetDataThread) {
                IsBackground = true
            };
            threadGetData.Start();
        }

        private void ClearLabel() {
            label_cpu.Content = string.Empty;
            label_memory.Content = string.Empty;
            label_read.Content = string.Empty;
            label_write.Content = string.Empty;
        }

        private void GetDataThread() {
            Process[] p = Process.GetProcessesByName("python");
            int interval = 100;
            TimeSpan prevCpuTime = TimeSpan.Zero;


            string pn = p[1].ProcessName;
            //PerformanceCounter cpuCounter = new PerformanceCounter("Process", "% Processor Time", pn);
            //PerformanceCounter ramCounter = new PerformanceCounter("Process", "Working Set", pn);
            PerformanceCounter readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec", pn);
            PerformanceCounter writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec", pn);

            Process pro = p[1];
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
