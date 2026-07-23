namespace Tww3Companion.Application.Importing;

internal static class ImportHandoffService
{
  public static Task<TResult> BuildPreviewAsync<TResult>(TResult result, Func<TResult, TResult> previewFactory)
  {
    ArgumentNullException.ThrowIfNull(result);
    ArgumentNullException.ThrowIfNull(previewFactory);

    return Task.FromResult(previewFactory(result));
  }

  public static async Task<TResult> ApplyAsync<TResult>(
      TResult result,
      bool confirm,
      Func<TResult, TResult> previewFactory,
      Action<TResult> validate,
      Func<TResult, TResult> appliedFactory)
  {
    ArgumentNullException.ThrowIfNull(previewFactory);
    ArgumentNullException.ThrowIfNull(validate);
    ArgumentNullException.ThrowIfNull(appliedFactory);

    var preview = await BuildPreviewAsync(result, previewFactory);
    if (!confirm) return preview;

    validate(preview);
    return appliedFactory(preview);
  }
}
