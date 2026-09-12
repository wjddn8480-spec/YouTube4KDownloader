namespace YouTube4KDownloader;
public static class VideoEncoders
{
    public static string[] Arguments(string encoder) => encoder switch
    {
        "cpu" => new[]{"-c:v","libx264","-preset","medium","-crf","18"},
        "amd" => new[]{"-c:v","h264_amf","-quality","quality","-rc","cqp","-qp_i","20","-qp_p","22"},
        "nvidia" => new[]{"-c:v","h264_nvenc","-preset","p5","-rc","vbr","-cq","20","-b:v","0"},
        "intel" => new[]{"-c:v","h264_qsv","-global_quality","20"},
        _ => throw new ArgumentException("Unknown encoder: " + encoder)
    };
}
