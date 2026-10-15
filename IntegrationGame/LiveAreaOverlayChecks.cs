using System;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveAreaOverlayChecks
{
    public static IEnumerator Run()
    {
        var areas=Singleton<GameAreaManager>.instance;
        int side=HundredTiles.Core.ExpandedMap.GridSize,count=HundredTiles.Core.ExpandedMap.TileCount;
        if(areas.m_areaCount!=count)throw new Exception("Area overlay fixture is not fully purchased.");
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();
        ToolsModifierControl.SetTool<GameAreaTool>();areas.SkipFading();
        try
        {
            GameAreaInfoPanel.Show(count-1);
            float edge=HundredTiles.Core.ExpandedMap.WorldMax-960;
            camera.ClearTarget();camera.enabled=true;camera.m_targetPosition=camera.m_currentPosition=new Vector3(edge,400,edge);
            camera.m_targetSize=camera.m_currentSize=2600;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);camera.m_targetHeight=camera.m_currentHeight=400;
            for(int i=0;i<90;i++)yield return null;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            areas.HighlightAreaIndex=count-1;typeof(GameAreaManager).GetMethod("UpdateAreaTexture",flags).Invoke(areas,null);
            var texture=(Texture2D)typeof(GameAreaManager).GetField("m_areaTex",flags).GetValue(areas);
            var material=(Material)typeof(GameAreaManager).GetField("m_areaMaterial",flags).GetValue(areas);
            int expectedSize=Mathf.NextPowerOfTwo(side+2);
            if(texture.width!=expectedSize || texture.height!=expectedSize)throw new Exception("Area texture dimensions differ.");
            for(int z=0;z<side;z++)for(int x=0;x<side;x++)
            {
                if(texture.GetPixel(x+1,z+1).r<.99f)throw new Exception("Area texture omitted a purchased tile: "+x+","+z);
                float minX,minZ,maxX,maxZ;areas.GetAreaBounds(x,z,out minX,out minZ,out maxX,out maxZ);
                if(minX!=HundredTiles.Core.ExpandedMap.WorldMin+x*1920 || minZ!=HundredTiles.Core.ExpandedMap.WorldMin+z*1920 || maxX-minX!=1920 || maxZ-minZ!=1920)throw new Exception("Purchased tile world bounds differ.");
            }
            var mapping=material.GetVector("_AreaMapping");float scale=1f/(expectedSize*1920),origin=(8640f+1920)*scale;
            if(Math.Abs(mapping.x-origin)>1e-6f || Math.Abs(mapping.y-origin)>1e-6f || Math.Abs(mapping.z-scale)>1e-8f || Math.Abs(mapping.w-1f/expectedSize)>1e-6f)throw new Exception("Area overlay GPU mapping differs: "+mapping);
            yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("trial324-purchase-overlay.png");
            Entry.Log("PASS: native area tool and last-tile info panel opened; all "+count+" purchased tile pixels and world bounds verified; "+expectedSize+"-square area texture/GPU mapping="+mapping+". Screenshot captured; physical purchase clicks are not asserted.");
        }
        finally{GameAreaInfoPanel.Hide();ToolsModifierControl.SetTool<DefaultTool>();}
    }
}
