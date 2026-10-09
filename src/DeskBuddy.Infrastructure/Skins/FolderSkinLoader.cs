using System.Text.Json;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Skins;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.Infrastructure.Skins;

/// <summary>
/// Loads a skin from a folder containing a <c>manifest.json</c> and frame images.
/// </summary>
/// <param name="logger">
/// Logger used to report skipped or invalid manifest entries.
/// </param>
public sealed class FolderSkinLoader(ILogger<FolderSkinLoader> logger) : ISkinLoader
{
    /// <summary>
    /// File name of the manifest inside a skin directory.
    /// </summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>
    /// Serializer options used to read the manifest.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public async Task<Skin> LoadAsync(string skinDirectory, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(skinDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException($"Skin manifest not found: {manifestPath}");
        }

        ManifestDto? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<ManifestDto>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Skin manifest is not valid JSON: {manifestPath}", ex);
        }

        if (manifest?.FrameSize is not { Length: 2 } size || manifest.States is null)
        {
            throw new InvalidDataException($"Skin manifest is missing 'frameSize' or 'states': {manifestPath}");
        }

        var animations = new Dictionary<PetState, SkinAnimation>();
        foreach (var (stateName, animation) in manifest.States)
        {
            if (!Enum.TryParse<PetState>(stateName, ignoreCase: true, out var state))
            {
                logger.LogWarning("Skin {Skin} defines unknown state '{State}'; ignoring it.", manifest.Name, stateName);
                continue;
            }

            animations[state] = BuildAnimation(skinDirectory, stateName, animation);
        }

        try
        {
            return new Skin(manifest.Name ?? Path.GetFileName(skinDirectory), size[0], size[1], animations);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"Skin manifest is invalid: {manifestPath}. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates a manifest animation entry and resolves its frame paths.
    /// </summary>
    /// <param name="skinDirectory">
    /// Skin directory used to resolve relative frame paths.
    /// </param>
    /// <param name="stateName">
    /// Name of the state, used in error messages.
    /// </param>
    /// <param name="animation">
    /// The animation entry from the manifest.
    /// </param>
    /// <returns>
    /// The resolved animation.
    /// </returns>
    private static SkinAnimation BuildAnimation(string skinDirectory, string stateName, AnimationDto? animation)
    {
        if (animation is not { Fps: > 0, Frames.Length: > 0 })
        {
            throw new InvalidDataException($"State '{stateName}' needs a positive 'fps' and at least one frame.");
        }

        var framePaths = new List<string>(animation.Frames.Length);
        foreach (var frame in animation.Frames)
        {
            var fullPath = Path.GetFullPath(Path.Combine(skinDirectory, frame));
            if (!File.Exists(fullPath))
            {
                throw new InvalidDataException($"Frame '{frame}' of state '{stateName}' not found: {fullPath}");
            }

            framePaths.Add(fullPath);
        }

        return new SkinAnimation(animation.Fps, framePaths);
    }

    /// <summary>
    /// JSON shape of the skin manifest.
    /// </summary>
    private sealed class ManifestDto
    {
        /// <summary>
        /// Gets or sets the skin display name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the frame size as <c>[width, height]</c>.
        /// </summary>
        public int[]? FrameSize { get; set; }

        /// <summary>
        /// Gets or sets the animations keyed by state name.
        /// </summary>
        public Dictionary<string, AnimationDto?>? States { get; set; }
    }

    /// <summary>
    /// JSON shape of a single animation entry.
    /// </summary>
    private sealed class AnimationDto
    {
        /// <summary>
        /// Gets or sets the playback speed in frames per second.
        /// </summary>
        public double Fps { get; set; }

        /// <summary>
        /// Gets or sets the frame file names relative to the skin directory.
        /// </summary>
        public string[]? Frames { get; set; }
    }
}
