using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AudioBooksPlayer.WPF.Annotations;
using AudioBooksPlayer.WPF.Streaming;
using PropertyChanged;
using System.Diagnostics;

namespace AudioBooksPlayer.WPF.ViewModel
{
    [ImplementPropertyChanged]
 
    public class Operation
    {
        public Guid OpId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public double Minimum { get; set; }
        public double CurrentValue { get; set; }
        public double Maximum { get; set; }

        public ICommand StopOperation { get; set; }
        public ICommand PauseOperation { get; set; }

        public event EventHandler OperatoinEnds;
    }



    public class OperationsViewModel
    {
        ObservableCollection<Operation> operations = new ObservableCollection<Operation>();
        private ConcurrentDictionary<Guid, bool> awaiting = new ConcurrentDictionary<Guid, bool>(); 

        public ObservableCollection<Operation> Operations => operations;
	    private ConcurrentDictionary<Operation, bool> opeartions; 

        bool isHaveOperation(Guid id)
        {
            return operations.ToArray().Any(x => x.OpId == id);
        }

        bool isAwaiting(Guid id)
        {
            return awaiting.ContainsKey(id);
        }

        Operation GetOperation(Guid id)
        {
            return operations.ToArray().FirstOrDefault(x => x.OpId == id);
        }

        public void AddOperation(StreamStatus status)
        {
            if (!isHaveOperation( status.operationId))
            {
	            awaiting.TryAdd(status.operationId, true);
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    operations.Add(new Operation()
                    {
                        OpId = status.operationId,
                        Name = status.Status.ToString(),
                        Type = status.Status.ToString()
                    });
	                bool val;
	                awaiting.TryRemove(status.operationId, out val);
                }
                    );
            }
            else
                throw new InvalidOperationException($"Operation with id {status.operationId} already registered");
        }

        public void RemoteOperation(Guid id)
        {
            if (!isHaveOperation(id))
                return;
            Application.Current.Dispatcher.InvokeAsync(() => {
                                                                 operations.Remove(operations.First(x => x.OpId == id));
            });
        }

        public void OperationStatusChanged(Guid id, double currentValue, double maxValue)
        {
            if (!isHaveOperation(id))
            {
                if (isAwaiting(id))
                    return;
                Debug.WriteLine($"Operation with id {id} not registered registered");
            }

            var oper = GetOperation(id);
            if (oper == null)
                return;
            oper.CurrentValue = currentValue;
            oper.Maximum = maxValue;
        }
    }
}
