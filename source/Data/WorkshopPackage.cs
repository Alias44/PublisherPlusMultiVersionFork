﻿using GitignoreParserNet;
using PublisherPlus.Patch;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Verse;
using Verse.Steam;
using Version = System.Version;

namespace PublisherPlus.Data
{
    /// <summary>
    /// A mod folder to be uploaded to the workshop. Handles most of the logic for file IO, aggregation, and filtering.
    /// </summary>
    internal class WorkshopPackage : WorkshopUploadable
  {
    private static readonly string separator = Path.DirectorySeparatorChar.ToString();

    private const string TempFolderName = "PublisherPlus\\Temp";
    private const string ConfigFileName = "_PublisherPlus.xml";
    private const string PublishedFileIdFilePath = "About\\PublishedFileId.txt";
    private const string GitIgnorePath = ".gitignore";

    private static readonly DirectoryInfo TempDirectory = new DirectoryInfo(Path.Combine(GenFilePaths.ConfigFolderPath, TempFolderName));

    private static WorkshopPackage _current;

    private readonly WorkshopItemHook _hook;
    private readonly Dictionary<FileSystemInfo, bool> _items = new Dictionary<FileSystemInfo, bool>();

    private PublishedFileId_t _id;
    public string Id => _id == PublishedFileId_t.Invalid ? Lang.Get("NewFileId") : _id.ToString();

    public string Title { get; set; }
    public string Description { get; set; }
    public List<string> Tags { get; private set; }
    public IEnumerable<Version> SupportedVersions { get; private set; }

    private FileInfo _previewFile;
    public string Preview
    {
      get => _previewFile.FullName;
      set
      {
        if (_previewFile?.FullName == value) { return; }
        _previewFile = new FileInfo(value);
      }
    }
    public bool PreviewExists => _previewFile.ExistsNow();
    public bool IsNewCreation => _id == PublishedFileId_t.Invalid;

    public DirectoryInfo SourceDirectory { get; private set; }
    public IEnumerable<FileSystemInfo> AllContent => _items.OrderByDescending(item => item.Value).Select(item => item.Key);

    private readonly DirectoryInfo _uploadDirectory;

    public WorkshopPackage(WorkshopItemHook hook)
    {
      _hook = hook;
      _id = hook.PublishedFileId;

      CopyHook();

      GetAllContent();
      GetConfig();

      _uploadDirectory = TempDirectory.CreateSubdirectory(SourceDirectory.Name);
      if (_uploadDirectory.ExistsNow()) { _uploadDirectory.Delete(true); }
      _uploadDirectory.Create();
    }

    private void CopyHook()
    {
      Title = _hook.Name;
      Description = _hook.Description;
      Tags = _hook.Tags?.ToList() ?? new List<string>();
      SupportedVersions = _hook.SupportedVersions;
      Preview = _hook.PreviewImagePath;
      SourceDirectory = new DirectoryInfo(_hook.Directory.FullName);
    }

    public bool IsIncluded(FileSystemInfo item) => !_items.ContainsKey(item) || _items[item];

    public void SetIncluded(FileSystemInfo item, bool value)
    {
      _items[item] = value;
      foreach (var key in _items.Keys.ToArray())
      {
        if (key.FullName.StartsWith(item.FullName)) { _items[key] = value; }
      }
    }

    /// <summary>
    /// Gets the path relative to the root <see cref="SourceDirectory">, if the item is a directory, appends a trailing slash
    /// </summary>
    public string GetRelativePath(FileSystemInfo item) => item.FullName.Substring(SourceDirectory.FullName.Length + 1) + (item.IsDirectory() ? separator : "");

    /// <summary>
    /// Sets the <see cref="_items"/> dictionary to all files and directories in <see cref="SourceDirectory"/> (excluding <see cref="ConfigFileName"/>)
    /// </summary>
    private void GetAllContent()
    {
      var contents = SourceDirectory.GetFileSystemInfos("*", SearchOption.AllDirectories)
                .OrderBy(item => item.FullName)
                .Where(item => item.Name != ConfigFileName);

      // apply .gitignore
      string ignoreFile = Path.Combine(SourceDirectory.FullName, GitIgnorePath);

      if (PublisherPlusSettings.useGitIgnore && File.Exists(ignoreFile))
      {
         var parse = new GitignoreParser(ignoreFile, Encoding.UTF8);

         contents = contents.Where(dir => parse.Accepts(separator + GetRelativePath(dir)));
      }


      _items.Clear();

      foreach (var path in contents)
      {
        _items.Add(path, true);
      }
    }

    /// <summary>
    /// Loads data from <see cref="ConfigFileName"/> and populates mod info and file exclusions
    /// </summary>
    private void GetConfig()
    {
      var configFile = Path.Combine(SourceDirectory.FullName, ConfigFileName);
      if (!File.Exists(configFile)) { return; }

      var xml = XDocument.Load(configFile).Root;
      if (xml == null) { return; }

      var ns = xml.Name.Namespace;

      var title = xml.Element(ns + "Title")?.Value;
      if (!title.NullOrEmpty()) { Title = title; }

      var tags = xml.Element(ns + "Tags")?.Elements().Select(element => element.Value).ToList();
      if (Mod.ExperimentalMode && tags != null && tags.Count > 0) { Tags = tags; }

      var preview = xml.Element(ns + "Preview")?.Value;
      if (!preview.NullOrEmpty()) { Preview = preview; }

      var exclusions = xml.Element(ns + "Excluded")?.Elements()
          .Select(element => element.Value)
          .Where(path => !path.NullOrEmpty())
          .Select(path => Path.Combine(SourceDirectory.FullName, path));

      if (exclusions == null) { return; }

      // find _items that match the loaded exclusion filters so that they can be exculded again
      var excludedPaths = _items.Keys.Where(item => exclusions.Any(exclude => item.FullName.StartsWith(exclude, StringComparison.OrdinalIgnoreCase)));

      foreach (var path in excludedPaths)
      {
          _items[path] = false;
      }
    }

    public bool HasContent() => _items.Any(item => item.Value);

    private IEnumerable<FileSystemInfo> GetExcluded() => _items.Where(item => !item.Value).Select(item => item.Key);

    /// <summary>
    /// Simplifies <see cref="GetExcluded"/> to remove unncessary branches
    /// </summary>
    private IEnumerable<string> GetExcludedPaths()
    {
      var list = new List<string>();
      foreach (var path in GetExcluded().Select(GetRelativePath).OrderBy(item => item))
      {
        if (list.Any(item => path.StartsWith(item))) { continue; }
        list.Add(path);
      }

      return list;
    }

    public void SaveConfig()
    {
      var configFile = Path.Combine(SourceDirectory.FullName, ConfigFileName);

      var xml = new XDocument();
      var root = new XElement("Configuration");
      xml.Add(root);
      if (Title != _hook.Name) { root.Add(new XElement("Title", Title)); }
      if (Mod.ExperimentalMode && !Tags.SequenceEqual(_hook.Tags)) { root.Add(new XElement("Tags", from tag in Tags select new XElement("tag", tag))); }
      if (Preview != _hook.PreviewImagePath && PreviewExists) { root.Add(new XElement("Preview", Preview)); }
      root.Add(new XElement("Excluded", from item in GetExcludedPaths() select new XElement("exclude", item)));

      xml.Save(configFile);
    }

    public void ResetConfig()
    {
      CopyHook();
      GetAllContent();
    }

    private void Prepare()
    {
      if (!PreviewExists) { Preview = _hook.PreviewImagePath; }

      foreach (var item in _items.Where(item => item.Value))
      {
        var path = Path.Combine(_uploadDirectory.FullName, GetRelativePath(item.Key));

        if (item.Key is DirectoryInfo) { new DirectoryInfo(path).Create(); }
        if (!(item.Key is FileInfo original)) { continue; }

        try
        {
          var destination = new FileInfo(path);
          if (destination.Directory == null) { throw new Mod.Exception("Destination directory is null"); }
          destination.Directory.Create();
          original.CopyTo(destination.FullName);
        }
        catch (Exception e)
        {
          var message = $"Skipping package file '{original.FullName}' due to error: {e.Message}";
          Mod.Warning(message);
        }
      }
    }

    public void Upload()
    {
      if (_current == this)
      {
        Mod.Error("This workshop package is still being uploaded");
        return;
      }
      _current = this;

      Prepare();

      Access.Method_Verse_Steam_Workshop_Upload_Call(this);
    }

    public static void OnUploaded()
    {
      if (_current == null) { return; }

      Mod.Log($"Finished uploading '{_current.Title}'");

      _current = null;
      TempDirectory.Delete(true);
    }

    public bool CanToUploadToWorkshop() => true;

    public void PrepareForWorkshopUpload()
    { }

    public PublishedFileId_t GetPublishedFileId() => _id;

    public void SetPublishedFileId(PublishedFileId_t pfid)
    {
      _id = pfid;

      var file = new FileInfo(Path.Combine(SourceDirectory.FullName, PublishedFileIdFilePath));
      _hook.PublishedFileId = pfid;

      if (_items.Keys.FirstOrDefault(item => item.FullName == file.FullName) != null) { return; }

      _items.Add(file, true);
    }

    public string GetWorkshopName() => Title;
    public string GetWorkshopDescription() => Description;
    public string GetWorkshopPreviewImagePath() => Preview;
    public IList<string> GetWorkshopTags() => Tags;
    public DirectoryInfo GetWorkshopUploadDirectory() => _uploadDirectory;
    public WorkshopItemHook GetWorkshopItemHook() => new WorkshopItemHook(this);
  }
}
