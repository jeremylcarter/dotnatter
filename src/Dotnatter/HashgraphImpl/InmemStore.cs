﻿using System.Collections.Generic;
using Dotnatter.Common;
using Dotnatter.Util;
using Serilog;

namespace Dotnatter.HashgraphImpl
{
    public class InmemStore : IStore
    {
        private readonly int cacheSize;
        private readonly ILogger logger;
        private readonly Dictionary<string, int> participants;
        private LruCache<string, Event> eventCache;
        private LruCache<int, RoundInfo> roundCache;
        private RollingIndex<string> consensusCache;
        private int totConsensusEvents;
        private readonly ParticipantEventsCache participantEventsCache;
        private Dictionary<string, Root> roots;
        private int lastRound;
        
        public InmemStore(Dictionary<string, int> participants, int cacheSize, ILogger logger)
        {
            var rts = new Dictionary<string, Root>();

            foreach (var p in participants)
            {
                rts.Add(p.Key,  Root.NewBaseRoot());
            }

            this.participants = participants;
            this.cacheSize = cacheSize;
            this.logger = logger.AddNamedContext("InmemStore");
            eventCache = new LruCache<string, Event>(cacheSize, null, logger,"EventCache");
            roundCache = new LruCache<int, RoundInfo>(cacheSize, null, logger,"RoundCache");
            consensusCache = new RollingIndex<string>(cacheSize);
            participantEventsCache = new ParticipantEventsCache(cacheSize, participants,logger);
            roots = rts;
            lastRound = -1;
        }

        public int CacheSize()
        {
            return cacheSize;
        }

        public (Dictionary<string, int> participents, StoreError err) Participants()
        {
            return (participants,null);
        }

        public (Event evt, StoreError err) GetEvent(string key)
        {
            bool ok=false;
            Event res=null;
            if (!string.IsNullOrEmpty(key))
            {
                (res,ok ) = eventCache.Get(key);
                logger.Verbose("GetEvent found={ok}; key={key}",ok,key);
            }
            
            if (!ok)
            {
                return (new Event(), new StoreError(StoreErrorType.KeyNotFound, key));
                
            }
            
            return (res,null);
        }

        public StoreError SetEvent(Event ev)
        {
            var key = ev.Hex();
            var (_, err) = GetEvent(key);

            if (err != null && err.StoreErrorType != StoreErrorType.KeyNotFound)
            {
                return err;
            }

            if (err != null && err.StoreErrorType == StoreErrorType.KeyNotFound)
            {
                err =AddParticpantEvent(ev.Creator, key, ev.Index());
                if (err != null)
                {
                    return err;
                }
            }
            
            eventCache.Add(key, ev);

            return null;

        }

        private StoreError AddParticpantEvent(string participant, string hash, int index)
        {
          return  participantEventsCache.Add(participant, hash, index);
        }

        public (string[] evts, StoreError err) ParticipantEvents(string participant, int skip)
        {
            return participantEventsCache.Get(participant, skip);
        }

        public (string ev, StoreError err) ParticipantEvent(string particant, int index)
        {
            return participantEventsCache.GetItem(particant, index);
        }

        public (string last, bool isRoot, StoreError err) LastFrom(string participant)
        {
            
            //try to get the last event from this participant
            var (last, err) = participantEventsCache.GetLast(participant);
            
            var isRoot = false;
            if (err != null)
            {
                return (last, isRoot, err);
            }

            //if there is none, grab the root
            if (last =="")
            {
                var ok = roots.TryGetValue(participant, out var root);

                if (ok)
                {
                    last = root.X;
                    isRoot = true;
                }
                else
                {
                    err=new  StoreError(StoreErrorType.NoRoot, participant);
                }
            }

            return (last, isRoot,err);
        }

        public Dictionary<int, int> Known()
        {
            return participantEventsCache.Known();
        }

        public string[] ConsensusEvents()
        {
            var (lastWindow, _) = consensusCache.GetLastWindow();

            var res = new List<string>();
            foreach (var item in lastWindow)
            {
                res.Add(item);
            }
            return res.ToArray();
        }

        public int ConsensusEventsCount()
        {
            return totConsensusEvents;
        }

        public StoreError AddConsensusEvent(string key)
        {
            consensusCache.Add(key, totConsensusEvents);
            totConsensusEvents++;
            return null;
        }

        public (RoundInfo roundInfo, StoreError err) GetRound(int r)
        {
            var (res, ok) = roundCache.Get(r);

            if (!ok)
            {
                return (new RoundInfo(), new StoreError(StoreErrorType.KeyNotFound, r.ToString())); ;
            }
            return (res,null);
        }

        public  StoreError  SetRound(int r, RoundInfo round)
        {
            roundCache.Add(r, round);

            if (r > lastRound)
            {
                lastRound = r;
            }

            return null;

        }

        public int LastRound()
        {
            return lastRound;
        }

        public string[] RoundWitnesses(int r)
        {
            var (round,err) = GetRound(r);

            if (err != null)
            {
                return new string[] { };
            }
            return round.Witnesses();
        }

        public int RoundEvents(int i)
        {
            var (round,err) = GetRound(i);
            if (err != null)
            {
                return 0;
            }
            return round.Events.Count;
        }

        public (Root root, StoreError err) GetRoot(string participant)
        {
            var ok = (roots.TryGetValue(participant, out var res));

            if (!ok)
            {
                return (new Root(), new StoreError(StoreErrorType.KeyNotFound, participant));
            }

            return (res,null);
        }

        public StoreError Reset(Dictionary<string, Root> newRoots)
        {
            roots = newRoots;

            eventCache = new LruCache<string, Event>(cacheSize, null, logger,"EventCache");

            roundCache = new LruCache<int, RoundInfo>(cacheSize, null, logger,"RoundCache");

            consensusCache = new RollingIndex<string>(cacheSize);

            var err = participantEventsCache.Reset();

            lastRound = -1;

            return err;
        }

        public StoreError Close()
        {
            return null;

        }
    }
}