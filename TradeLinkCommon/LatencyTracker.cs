﻿using TradeLink.Common;
using TradeLink.API;

namespace TradeLink.Common
{

    public delegate void LatencyDelegate(MessageTypes type, uint id, double timems);
    /// <summary>
    /// used to track latency between:
    /// orders->order acks
    /// orders->fills
    /// cancels->cancelack
    /// </summary>
    public class LatencyTracker
    {
        IdTracker _idt;
        System.Collections.Generic.Dictionary<uint, long> _otime = new System.Collections.Generic.Dictionary<uint, long>(100);
        System.Collections.Generic.Dictionary<uint, long> _ctime = new System.Collections.Generic.Dictionary<uint, long>(100);
        System.Collections.Generic.Dictionary<uint, string> _id2sym = new System.Collections.Generic.Dictionary<uint, string>();

        public void sendorder(Order o)
        {
            // ensure has id
            if (o.id == 0)
                o.id = _idt.AssignId;

            // make sure we have a time
            long time = o.time;
            if (time == 0)
                time = gettime();

            // update time
            long tmp;
            if (_otime.TryGetValue(o.id, out tmp))
                _otime[o.id] = time;
            else
            {
                _otime.Add(o.id, time);
                _id2sym.Add(o.id, o.symbol);
            }

            // chain order
            if (SendOrder != null)
                SendOrder(o);
        }
        public event UIntDelegate SendCancel;
        public void sendcancel(uint id)
        {
            long time = gettime();
            // store start for cancel
            long tmp;
            if (!_ctime.TryGetValue(id, out tmp))
                _ctime.Add(id,time);

            // chain
            if (SendCancel != null)
                SendCancel(id);
        }

        long gettime()
        {
            return System.DateTime.Now.Ticks;
        }

        public event LatencyDelegate SendLatency;
        public event OrderDelegate SendOrder;

        public void GotFill(Trade t)
        {
            if (SendLatency == null) return;
            // see if we know this message
            long start = 0;
            if (!_otime.TryGetValue(t.id, out start)) return;
            double lat = new System.DateTime(gettime()).Subtract(new System.DateTime(start)).TotalMilliseconds;
            report(MessageTypes.EXECUTENOTIFY, t.id, lat);
        }

        public void GotOrder(Order o)
        {
            if (SendLatency == null) return;
            // see if we know this message
            long start = 0;
            if (!_otime.TryGetValue(o.id, out start)) return;
            double lat = new System.DateTime(gettime()).Subtract(new System.DateTime(start)).TotalMilliseconds;
            report(MessageTypes.ORDERNOTIFY, o.id, lat);
        }

        public void GotCancel(int id) { GotCancel((uint)id); }
        public void GotCancel(long id) { GotCancel((uint)id); }
        public void GotCancel(uint id)
        {
            
            // see if we know this message
            long start = 0;
            if (!_ctime.TryGetValue(id, out start)) return;
            double lat = new System.DateTime(gettime()).Subtract(new System.DateTime(start)).TotalMilliseconds;
            report(MessageTypes.ORDERCANCELRESPONSE, id, lat);

        }

        void report(MessageTypes type, uint id, double last)
        {
            if (SendLatency == null) return;
            SendLatency(type, id, last);
            if (_logit)
            {
                string sym = string.Empty;
                _id2sym.TryGetValue(id,out sym);
                try
                {
                    if (_log == null)
                    {
                        bool exists = System.IO.File.Exists(_logpath);
                        _log = new System.IO.StreamWriter(_logpath, true);
                        if (!exists)
                            _log.WriteLine("Symbol,Message,Id,Latency");
                        _log.AutoFlush = true;
                    }
                    _log.WriteLine(string.Format("{0},{1},{2},{3:F1}", sym, type.ToString(), id, last));
                }
                catch { }
            }

        }
        string _logpath;
        bool _logit = false;
        System.IO.StreamWriter _log;
        public LatencyTracker() : this(null,new IdTracker()) { }
        public LatencyTracker(string logpath, IdTracker idt)
        {
            if (logpath != null)
            {
                try
                {
                    _logit = true;
                    _logpath = logpath;
                }
                catch
                {
                    _log = null;
                    _logit = false;
                }
            }
            _idt = idt;
               
        }

        public void Stop()
        {
            if (_logit && (_log != null))
            {
                _log.Close();
                _log = null;
            }
        }

        public void Reset()
        {
            _log.Close();
            _otime.Clear();
            _ctime.Clear();
        }
    }
}
