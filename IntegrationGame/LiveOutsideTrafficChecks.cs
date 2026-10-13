using System;
using ColossalFramework;
using HundredTiles.Runtime;

static class LiveOutsideTrafficChecks
{
    public static System.Collections.IEnumerator CheckVehicles()
    {
        yield return new UnityEngine.WaitForSeconds(15f);
        bool finished=false;Exception failure=null;
        Singleton<SimulationManager>.instance.AddAction(delegate{
            try{
                int cars=0,trains=0;foreach(var data in Singleton<VehicleManager>.instance.m_vehicles.m_buffer)if((data.m_flags&(Vehicle.Flags.Created|Vehicle.Flags.DummyTraffic|Vehicle.Flags.Spawned))==(Vehicle.Flags.Created|Vehicle.Flags.DummyTraffic|Vehicle.Flags.Spawned) && data.Info!=null){if(data.Info.m_vehicleType==VehicleInfo.VehicleType.Car)cars++;if(data.Info.m_vehicleType==VehicleInfo.VehicleType.Train)trains++;}
                Entry.Log("Live forced outside vehicles after native simulation: spawned cars="+cars+", spawned trains="+trains);
                if(cars==0 || trains==0)throw new Exception("Forced outside cars/trains were not spawned after native routing.");
                Entry.Log("PASS: native simulation spawned demand-independent outside road vehicles and trains. Full journey and station stops are not asserted.");
            }catch(Exception error){failure=error;}finally{finished=true;}
        });
        while(!finished)yield return null;
        if(failure!=null){Entry.Log("FAILURE: "+failure);System.IO.File.WriteAllText(System.IO.Path.Combine(Entry.Root,"failed.marker"),failure.ToString());}
        UnityEngine.Application.Quit();
    }
    public static void Run()
    {
        var manager=Singleton<VehicleManager>.instance;
        int beforeCars=DummyCitizens(),beforeTrains=DummyTrains();int requests=0;
        for(int i=0;i<8;i++){int wave=OutsideTraffic.DispatchWave();if(wave>2)throw new Exception("Outside wave exceeded its bound.");requests+=wave;}
        int cars=DummyCitizens()-beforeCars,trains=DummyTrains()-beforeTrains;
        Entry.Log("Forced outside traffic: requests="+requests+", dummy citizen instances added="+cars+", dummy train vehicles added="+trains);
        if(cars<=0 || trains<=0)throw new Exception("Native demand-independent road/train transfers were not created.");
        var buffer=manager.m_vehicles.m_buffer;
        foreach(var data in buffer)if((data.m_flags&(Vehicle.Flags.Created|Vehicle.Flags.DummyTraffic))==(Vehicle.Flags.Created|Vehicle.Flags.DummyTraffic) && data.Info!=null && data.Info.m_vehicleType==VehicleInfo.VehicleType.Train){if(data.m_sourceBuilding==0 || data.m_targetBuilding==0)throw new Exception("Dummy train lacks native source/target.");}
        Entry.Log("PASS: demand-independent native dummy road citizens and train vehicles created through registered outside connections; bounded waves and native train source/target preserved. Vehicle arrival is not asserted.");
    }
    static int DummyCitizens(){int result=0;var citizens=Singleton<CitizenManager>.instance;foreach(var data in citizens.m_instances.m_buffer)if((data.m_flags&CitizenInstance.Flags.Created)!=0 && data.m_citizen!=0 && (citizens.m_citizens.m_buffer[data.m_citizen].m_flags&Citizen.Flags.DummyTraffic)!=0)result++;return result;}
    static int DummyTrains(){int result=0;foreach(var data in Singleton<VehicleManager>.instance.m_vehicles.m_buffer)if((data.m_flags&(Vehicle.Flags.Created|Vehicle.Flags.DummyTraffic))==(Vehicle.Flags.Created|Vehicle.Flags.DummyTraffic) && data.Info!=null && data.Info.m_vehicleType==VehicleInfo.VehicleType.Train)result++;return result;}
}
