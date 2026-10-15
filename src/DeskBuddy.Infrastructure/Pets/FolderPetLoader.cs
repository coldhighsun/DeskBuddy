using System.Buffers.Binary;
using System.Text.Json;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Infrastructure.Pets;

/// <summary>
/// Loads a pet from a folder containing a <c>manifest.json</c> and the atlas image that holds all its frames.
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
            throw new InvalidDataException(
                $"Pet manifest is not valid JSON, or a state's 'frames' is not a list of atlas cell numbers (frames written as file names are no longer supported; declare an 'atlas' and list the numbers of its cells): {manifestPath}",
                ex);
        }

        if (manifest?.FrameSize is not { Length: 2 } size || manifest.States is null)
        {
            throw new InvalidDataException($"Pet manifest is missing 'frameSize' or 'states': {manifestPath}");
        }

        var atlas = ReadAtlas(petDirectory, manifest.Atlas, size, manifestPath);

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
                    gazeAnimations[gaze] = BuildAnimation(atlas, stateName, animation);
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

            animations[state] = BuildAnimation(atlas, stateName, animation);
        }

        try
        {
            return new Pet(manifest.Name ?? Path.GetFileName(petDirectory), size[0], size[1], animations, gazeAnimations, manifest.Names);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"Pet manifest is invalid: {manifestPath}. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads the atlas section of the manifest and checks the atlas image: it must be there, be a PNG, and have room for
    /// the columns of cells the manifest declares.
    /// </summary>
    /// <param name="petDirectory">
    /// Pet directory used to resolve the image.
    /// </param>
    /// <param name="dto">
    /// The atlas section of the manifest.
    /// </param>
    /// <param name="frameSize">
    /// The frame size from the manifest as <c>[width, height]</c>, which the cell must have the proportions of.
    /// </param>
    /// <param name="manifestPath">
    /// Path of the manifest, used in error messages.
    /// </param>
    /// <returns>
    /// The atlas.
    /// </returns>
    private static Atlas ReadAtlas(string petDirectory, AtlasDto? dto, int[] frameSize, string manifestPath)
    {
        if (dto is not { Image: { Length: > 0 } image, Cell: { Length: 2 } cell } || cell[0] <= 0 || cell[1] <= 0)
        {
            throw new InvalidDataException($"Pet manifest is missing a valid 'atlas' with an 'image' and a 'cell' of [width, height]: {manifestPath}");
        }

        // The window draws every frame at 'frameSize', so a cell of other proportions would show the pet squashed.
        if ((long)frameSize[0] * cell[1] != (long)frameSize[1] * cell[0])
        {
            throw new InvalidDataException($"The atlas cell {cell[0]}x{cell[1]} does not have the proportions of the frame size {frameSize[0]}x{frameSize[1]}: {manifestPath}");
        }

        var path = Path.GetFullPath(Path.Combine(petDirectory, image));
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Atlas image '{image}' not found: {path}");
        }

        var (width, height) = ReadPngSize(path);
        var columns = dto.Columns ?? (width / cell[0]);
        if (columns <= 0 || (long)columns * cell[0] > width || cell[1] > height)
        {
            throw new InvalidDataException($"Atlas image '{image}' is {width}x{height}, too small for {columns} columns of {cell[0]}x{cell[1]} cells (at least one row of them must fit).");
        }

        return new Atlas(path, cell[0], cell[1], columns, height / cell[1]);
    }

    /// <summary>
    /// Reads the size of a PNG image from its header, without decoding it.
    /// </summary>
    /// <param name="path">
    /// The PNG file.
    /// </param>
    /// <returns>
    /// The width and the height in pixels.
    /// </returns>
    private static (int Width, int Height) ReadPngSize(string path)
    {
        // The signature (8 bytes), then the IHDR chunk: its length and name (8 bytes), then the width and the height.
        const int HeaderLength = 24;
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var header = new byte[HeaderLength];
        using (var stream = File.OpenRead(path))
        {
            try
            {
                stream.ReadExactly(header);
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException($"The atlas image is too short to be a PNG file: {path}", ex);
            }
        }

        if (!header.AsSpan(0, signature.Length).SequenceEqual(signature) || !header.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException($"The atlas image is not a PNG file: {path}");
        }

        return (BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
    }

    /// <summary>
    /// Validates a manifest animation entry and works out where its frames are in the atlas.
    /// </summary>
    /// <param name="atlas">
    /// The atlas the frames are cells of.
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
    private static PetAnimation BuildAnimation(Atlas atlas, string stateName, AnimationDto? animation)
    {
        if (animation is not { Fps: > 0, Frames.Length: > 0 })
        {
            throw new InvalidDataException($"State '{stateName}' needs a positive 'fps' and at least one frame.");
        }

        var cellCount = (long)atlas.Columns * atlas.Rows;
        var framePaths = new List<string>(animation.Frames.Length);
        foreach (var cell in animation.Frames)
        {
            if (cell < 0 || cell >= cellCount)
            {
                throw new InvalidDataException($"Frame {cell} of state '{stateName}' is not a cell of the atlas, which has {cellCount}.");
            }

            framePaths.Add(FrameId.Create(atlas.Path, (cell % atlas.Columns) * atlas.CellWidth, (cell / atlas.Columns) * atlas.CellHeight, atlas.CellWidth, atlas.CellHeight));
        }

        return new PetAnimation(animation.Fps, framePaths);
    }

    /// <summary>
    /// The atlas of a pet: one image that holds all its frames in a grid of equal cells.
    /// </summary>
    /// <param name="Path">
    /// The full path of the image.
    /// </param>
    /// <param name="CellWidth">
    /// The width of a cell in pixels.
    /// </param>
    /// <param name="CellHeight">
    /// The height of a cell in pixels.
    /// </param>
    /// <param name="Columns">
    /// The number of cells in a row.
    /// </param>
    /// <param name="Rows">
    /// The number of rows of cells that fit in the image.
    /// </param>
    private sealed record Atlas(string Path, int CellWidth, int CellHeight, int Columns, int Rows);

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
        /// Gets or sets the pet's name in other languages, keyed by culture name or language, such as <c>zh</c> or <c>en</c>.
        /// </summary>
        public Dictionary<string, string>? Names { get; set; }

        /// <summary>
        /// Gets or sets the frame size as <c>[width, height]</c>.
        /// </summary>
        public int[]? FrameSize { get; set; }

        /// <summary>
        /// Gets or sets the atlas image that holds all the frames.
        /// </summary>
        public AtlasDto? Atlas { get; set; }

        /// <summary>
        /// Gets or sets the animations keyed by state name.
        /// </summary>
        public Dictionary<string, AnimationDto?>? States { get; set; }
    }

    /// <summary>
    /// JSON shape of the atlas section.
    /// </summary>
    private sealed class AtlasDto
    {
        /// <summary>
        /// Gets or sets the image file name relative to the pet directory.
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// Gets or sets the size of a cell in pixels as <c>[width, height]</c>.
        /// </summary>
        public int[]? Cell { get; set; }

        /// <summary>
        /// Gets or sets the number of cells in a row; if absent, as many as fit in the width of the image.
        /// </summary>
        public int? Columns { get; set; }
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
        /// Gets or sets the frames as the numbers of their cells in the atlas, counted along the rows from the top left.
        /// </summary>
        public int[]? Frames { get; set; }
    }
}
