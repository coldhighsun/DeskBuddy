using System.Text.Json;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Pets;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.Infrastructure.Pets;

/// <summary>
/// Loads a pet from a folder containing a <c>manifest.json</c> and frame images.
/// </summary>
/// <param name="logger">
/// Logger used to report skipped or invalid manifest entries.
/// </param>
public sealed class FolderPetLoader(ILogger<FolderPetLoader> logger) : IPetLoader
{
    /// <summary>
    /// File name of the manifest inside a pet directory.
    /// </summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>
    /// Start of the manifest keys that name the looks of the Idle state, such as <c>Idle.Left</c>.
    /// </summary>
    private const string IdlePrefix = "Idle.";

    /// <summary>
    /// Serializer options used to read the manifest.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public async Task<Pet> LoadAsync(string petDirectory, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(petDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException($"Pet manifest not found: {manifestPath}");
        }

        ManifestDto? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<ManifestDto>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Pet manifest is not valid JSON: {manifestPath}", ex);
        }

        if (manifest?.FrameSize is not { Length: 2 } size || manifest.States is null)
        {
            throw new InvalidDataException($"Pet manifest is missing 'frameSize' or 'states': {manifestPath}");
        }

        var animations = new Dictionary<PetState, PetAnimation>();
        var gazeAnimations = new Dictionary<GazeDirection, PetAnimation>();
        foreach (var (stateName, animation) in manifest.States)
        {
            // "Idle.Left" and the like are the variants of Idle for looking in a direction.
            if (stateName.StartsWith(IdlePrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<GazeDirection>(stateName[IdlePrefix.Length..], ignoreCase: true, out var gaze)
                    && gaze != GazeDirection.None)
                {
                    gazeAnimations[gaze] = BuildAnimation(petDirectory, stateName, animation);
                }
                else
                {
                    logger.LogWarning("Pet {Pet} defines unknown look '{State}'; ignoring it.", manifest.Name, stateName);
                }

                continue;
            }

            if (!Enum.TryParse<PetState>(stateName, ignoreCase: true, out var state))
            {
                logger.LogWarning("Pet {Pet} defines unknown state '{State}'; ignoring it.", manifest.Name, stateName);
                continue;
            }

            animations[state] = BuildAnimation(petDirectory, stateName, animation);
        }

        try
        {
            return new Pet(manifest.Name ?? Path.GetFileName(petDirectory), size[0], size[1], animations, gazeAnimations);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"Pet manifest is invalid: {manifestPath}. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates a manifest animation entry and resolves its frame paths.
    /// </summary>
    /// <param name="petDirectory">
    /// Pet directory used to resolve relative frame paths.
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
    private static PetAnimation BuildAnimation(string petDirectory, string stateName, AnimationDto? animation)
    {
        if (animation is not { Fps: > 0, Frames.Length: > 0 })
        {
            throw new InvalidDataException($"State '{stateName}' needs a positive 'fps' and at least one frame.");
        }

        var framePaths = new List<string>(animation.Frames.Length);
        foreach (var frame in animation.Frames)
        {
            var fullPath = Path.GetFullPath(Path.Combine(petDirectory, frame));
            if (!File.Exists(fullPath))
            {
                throw new InvalidDataException($"Frame '{frame}' of state '{stateName}' not found: {fullPath}");
            }

            framePaths.Add(fullPath);
        }

        return new PetAnimation(animation.Fps, framePaths);
    }

    /// <summary>
    /// JSON shape of the pet manifest.
    /// </summary>
    private sealed class ManifestDto
    {
        /// <summary>
        /// Gets or sets the pet display name.
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
        /// Gets or sets the frame file names relative to the pet directory.
        /// </summary>
        public string[]? Frames { get; set; }
    }
}
