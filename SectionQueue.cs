namespace YouTube4KDownloader;
public static class SectionQueue
{
    public static void Expand(IList<DownloadQueueItem> queue,IEnumerable<DownloadQueueItem> pending,IReadOnlyList<ClipSelection> sections,string waiting)
    {
        if(sections.Count==0)return;
        if(sections.Count>100)throw new ArgumentException(LocalizationManager.Get("SectionLimit"));
        foreach(var original in pending.Where(x=>!x.SectionsAssigned).ToArray())
        {
            int index=queue.IndexOf(original);if(index<0)continue;
            queue.RemoveAt(index);
            foreach(var section in sections.Distinct())queue.Insert(index++,new DownloadQueueItem{Url=original.Url,Title=original.Title,Status=waiting,Clip=section,SectionsAssigned=true});
        }
    }
}
