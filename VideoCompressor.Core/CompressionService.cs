using Xabe.FFmpeg;

namespace VideoCompressor.Core;

public sealed class CompressionService
{
    public async Task<CompressionItemResult> CompressAsync(
        CompressionOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        try
        {
            IMediaInfo info = await FFmpeg.GetMediaInfo(options.InputPath, cancellationToken);

            var conversion = FFmpeg.Conversions.New()
                .SetOutput(options.OutputPath)
                .SetOverwriteOutput(true);

            var video = info.VideoStreams.FirstOrDefault();
            if (video != null)
            {
                video.SetCodec(VideoCodec.h264);
                conversion.AddStream(video);
            }

            var audio = info.AudioStreams.FirstOrDefault();
            if (audio != null)
            {
                audio.SetCodec(AudioCodec.aac);
                conversion.AddStream(audio);
            }

            conversion
                .AddParameter($"-crf {options.Crf}", ParameterPosition.PostInput)
                .AddParameter($"-preset {options.Preset}", ParameterPosition.PostInput)
                .AddParameter("-b:a 128k", ParameterPosition.PostInput)
                .AddParameter("-movflags +faststart", ParameterPosition.PostInput);

            if (options.TargetHeight > 0 && video != null)
                conversion.AddParameter($"-vf scale=-2:{options.TargetHeight}", ParameterPosition.PostInput);

            if (progress != null)
            {
                conversion.OnProgress += (_, args) =>
                    progress.Report(Math.Clamp(args.Percent, 0, 100));
            }

            await conversion.Start(cancellationToken);

            if (File.Exists(options.OutputPath))
                return CompressionItemResult.Done(options.InputPath, options.OutputPath);

            return CompressionItemResult.Error(options.InputPath, "Output file was not created.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CompressionItemResult.Error(options.InputPath, ex.Message);
        }
    }
}
