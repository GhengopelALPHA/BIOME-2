using System;
using System.IO;
using System.Collections.Generic;
using Biome2.Diagnostics;
using Biome2.FileLoading.Models;
using static Biome2.World.CellGrid.GridTopologies;

namespace Biome2.FileLoading;

/// <summary>
/// Placeholder for loading a rules file.
/// The UI will call into this service later (open file dialog, recent files, drag drop).
/// </summary>
public sealed class RulesLoader {
    public static WorldModel Load(string path) {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        var lines = File.ReadAllLines(path);

        // Track current scanning position for improved error reporting.
        int _currentLineNo = 0;
        string _currentRawLine = string.Empty;

        int GetPosition(string? substring) {
            if (string.IsNullOrEmpty(_currentRawLine) || string.IsNullOrEmpty(substring))
                return 1;
            int idx = _currentRawLine.IndexOf(substring, StringComparison.Ordinal);
            return Math.Max(1, idx + 1);
        }

        void LogLineParseError(string reason, string? substringForPos = null) {
            var lineInfo = string.IsNullOrEmpty(_currentRawLine) ? string.Empty : $"Line=\"{_currentRawLine}\"";
            int pos = GetPosition(substringForPos);
            Logger.Error($"Parse error at line {_currentLineNo}, pos {pos}: {reason}. {lineInfo}");
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var species = new List<SpeciesModel>();
        var layers = new List<string>();
        var rules = new List<RulesModel>();

        int section = 0; // 0=settings,1=species,2=layers,3=rules

        for (int i = 0; i < lines.Length; i++) {
            _currentLineNo =  i + 1;
            _currentRawLine = (string?) lines[i] ?? string.Empty;
            var line = _currentRawLine;

            // strip comments
            var semi = line.IndexOf(';');
            if (semi >= 0)
                line = line[..semi];

            var readLine = line.Trim();

            if (readLine.Length == 0)
                continue;
            if (readLine == "%%") { section++; continue; }

            if (section == 0) {
                // settings like "WIDTH = 150"
                var eq = readLine.IndexOf('=');
                if (eq > 0) {
                    var k = readLine[..eq].Trim();
                    var v = readLine[(eq + 1)..].Trim();
                    settings[k] = v;
                } else {
                    LogLineParseError("Invalid setting (missing '=')", readLine);
                }
            } else if (section == 1) {
                // species lines like "NAME = {r,g,b}"
                var eq = readLine.IndexOf('=');
                if (eq <= 0) {
                    LogLineParseError("Invalid species definition (missing '=')", readLine);
                    continue;
                }
                var name = readLine[..eq].Trim();
                var rest = readLine[(eq + 1)..].Trim();

                // find braces
                var l = rest.IndexOf('{');
                var r = rest.IndexOf('}');
                if (l >= 0 && r > l) {
                    var content = rest.Substring(l + 1, r - l - 1);
                    var parts = content.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    byte[] col = [0, 0, 0, 255];
                    for (int j = 0; j < Math.Min(parts.Length, 4); j++) {
                        if (byte.TryParse(parts[j], out var b))
                            col[j] = b;
                        else {
                            LogLineParseError($"Invalid color component '{parts[j]}'", parts[j]);
                        }
                    }
                    species.Add(new SpeciesModel(name, col, _currentRawLine));
                } else {
                    LogLineParseError("Invalid species definition (missing '{' or '}')", rest);
                }
            } else if (section == 2) {
                // layers: currently expect lines like "DISCRETE NAME"
                var parts = readLine.Split((char[])null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && string.Equals(parts[0], "DISCRETE", StringComparison.OrdinalIgnoreCase)) {
                    layers.Add(parts[1]);
                } else {
                    LogLineParseError("Invalid layer definition (expected 'DISCRETE NAME')", readLine);
                }
            } else {
                // rules section. parse basic form layer:origin [reactants] -> new*prob
                // Example: FOREST:OLD + 1FIRE2+ -> FIRE1*0.1
                var arrow = readLine.IndexOf("->", StringComparison.Ordinal);
                if (arrow <= 0) { LogLineParseError("Invalid rule (missing '->')", readLine); continue; }
                var left = readLine[..arrow].Trim();
                var right = readLine[(arrow + 2)..].Trim();

                // left: optional coordinate limits then layer:origin [reactants]
                // Coordinate limits may be parenthesized like "(0:60,40:90)" or as a simple prefix "0:30"
                int? xMin = null, xMax = null, yMin = null, yMax = null;

                // Helper to parse a single axis spec like "0:60" or ":30" or "40:"
                static bool TryParseAxis(string spec, out int? aMin, out int? aMax) {
                    aMin = null; aMax = null;
                    if (string.IsNullOrWhiteSpace(spec)) return true;
                    var parts = spec.Split(':', 2);
                    if (parts.Length != 2) return false;
                    var smin = parts[0].Trim();
                    var smax = parts[1].Trim();
                    if (smin.Length > 0) {
                        if (int.TryParse(smin, out var vmin)) aMin = vmin;
                        else return false;
                    }
                    if (smax.Length > 0) {
                        if (int.TryParse(smax, out var vmax)) aMax = vmax;
                        else return false;
                    }
                    return true;
                }

                // Detect parenthesized coords at start
                var leftTrim = left.TrimStart();
                if (leftTrim.StartsWith("(") ) {
                    int end = leftTrim.IndexOf(')');
                    if (end > 0) {
                        var coords = leftTrim.Substring(1, end - 1).Trim();
                        left = leftTrim.Substring(end + 1).TrimStart();
                        // split into x,y by comma
                        var parts = coords.Split(',', 2);
                        if (parts.Length >= 1) {
                            var xspec = parts[0].Trim();
                            if (!string.IsNullOrEmpty(xspec)) {
                                if (!TryParseAxis(xspec, out xMin, out xMax)) {
                                    LogLineParseError($"Invalid coordinate spec '{xspec}'", xspec);
                                    xMin = xMax = null;
                                }
                            }
                        }
                        if (parts.Length == 2) {
                            var yspec = parts[1].Trim();
                            if (!string.IsNullOrEmpty(yspec)) {
                                if (!TryParseAxis(yspec, out yMin, out yMax)) {
                                    LogLineParseError($"Invalid coordinate spec '{yspec}'", yspec);
                                    yMin = yMax = null;
                                }
                            }
                        }
                    }
                } else {
                    // Check for simple prefix token before first space
                    var firstSpace = left.IndexOf(' ');
                    string firstTok = firstSpace >= 0 ? left[..firstSpace] : left;
                    // Heuristic: treat as coord token when it contains ':' and has no letters
                    bool hasColon = firstTok.Contains(':');
                    bool hasLetter = false;
                    foreach (var ch in firstTok) if (char.IsLetter(ch)) { hasLetter = true; break; }
                    if (hasColon && !hasLetter) {
                        // consume token
                        left = (firstSpace >= 0) ? left.Substring(firstTok.Length).TrimStart() : string.Empty;
                        // token may contain comma separating x and y
                        var parts = firstTok.Split(',', 2);
                        if (parts.Length >= 1) {
                            if (!TryParseAxis(parts[0].Trim(), out xMin, out xMax)) {
                                LogLineParseError($"Invalid coordinate spec '{parts[0]}'", parts[0]);
                                xMin = xMax = null;
                            }
                        }
                        if (parts.Length == 2) {
                            if (!TryParseAxis(parts[1].Trim(), out yMin, out yMax)) {
                                LogLineParseError($"Invalid coordinate spec '{parts[1]}'", parts[1]);
                                yMin = yMax = null;
                            }
                        }
                    }
                }

                // left: layer:origin [reactants]
                var colon = left.IndexOf(':');
                string layerName;
                string remainder;
                if (colon > 0) {
                    layerName = left[..colon].Trim();
                    remainder = left[(colon + 1)..].Trim();

                } else {
                    // No explicit layer provided. Use the first defined layer if available.
                    remainder = left;
                    if (layers.Count > 0)
                        layerName = layers[0];
                    else {
                        LogLineParseError("No layer specified and no layers were defined. Rule will not apply", left);
                        layerName = String.Empty;
                    }
                }

                // origin species is first token until whitespace or operator
                var originParts = remainder.Split((char[])null, 2, StringSplitOptions.TrimEntries);
                if (originParts.Length == 0 || string.IsNullOrEmpty(originParts[0])) {
                    LogLineParseError("Missing origin species", remainder);
                    continue;
                }
                var originSpecies = originParts[0];

                var reactants = Array.Empty<ReactantModel>();
                if (originParts.Length > 1) {
                    // parse reactants string like "+ 1FIRE2+"
                    var reactStr = originParts[1].Trim();
                    var list = new List<ReactantModel>();

                    // reactants are separated by whitespace (space, tabs, etc.)
                    var tokens = reactStr.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    // e.g. +, -, 1FIRE2+ or SIMULATOR:COLOR1
                    bool pendingExclusion = false;
                    foreach (var tok in tokens) {
                        if (tok == "+") { pendingExclusion = false; continue; }
                        if (tok == "-") { pendingExclusion = true; continue; }

                        // determine sign from trailing + or - (this is different from a leading '-' token)
                        int sign = 0;
                        if (tok.EndsWith('+')) sign = 1;
                        else if (tok.EndsWith('-')) sign = -1;

                        var core = tok.TrimEnd('+', '-');

                        // core might start with a number (count) or be a layer-prefixed name
                        int idx = 0;
                        while (idx < core.Length && char.IsDigit(core[idx])) idx++;

                        int count = 0; // default: no count specified
                        if (idx > 0) {
                            if (!int.TryParse(core.AsSpan(0, idx), out count)) {
                                LogLineParseError($"Invalid reactant count '{core[..idx]}'", core[..idx]);
                                // reset pending exclusion and skip
                                pendingExclusion = false;
                                continue;
                            }
                            // validate count
                            if (count < 0) {
                                LogLineParseError($"Reactant count must be positive, got '{count}'", core[..idx]);
                                pendingExclusion = false;
                                continue;
                            } else if (count > 8) {
                                LogLineParseError($"Reactant count too large for given RANGE, got '{count}'", core[..idx]); // TODO: RANGE setting
                            }
                        }

                        // species part may include an explicit layer like LAYER:SPECIES
                        var speciesPart = core[idx..];
                        string reactLayer = string.Empty;
                        string speciesName = speciesPart;
                        var colonPos = speciesPart.IndexOf(':');
                        if (colonPos >= 0) {
                            reactLayer = speciesPart[..colonPos];
                            speciesName = speciesPart[(colonPos + 1)..];
                        }

                        if (string.IsNullOrEmpty(speciesName)) {
                            LogLineParseError("Invalid reactant (missing species name)", core);
                            pendingExclusion = false;
                            continue;
                        }

                        // If an exclusion prefix ('-') was present, validate that it's compatible
                        bool exclusion = false;
                        if (pendingExclusion) {
                            // Exclusionary reactants currently do not support explicit counts or trailing signs
                            if (count != 0) {
                                LogLineParseError($"Exclusionary reactant does not support counts '{core}'", core);
                                pendingExclusion = false;
                                continue;
                            }
                            if (sign != 0) {
                                LogLineParseError($"Exclusionary reactant does not support trailing '+' or '-' modifiers '{core}'", core);
                                pendingExclusion = false;
                                continue;
                            }
                            exclusion = true;
                        }

                        list.Add(new ReactantModel(speciesName, reactLayer, count, sign, exclusion));
                        pendingExclusion = false;
                    }
                    reactants = [.. list];
                }

                // right side: newSpecies * probability
                var prob = 1.0;
                var newSpec = right;
                var star = right.IndexOf('*');
                if (star >= 0) {
                    newSpec = right[..star].Trim();
                    var probStr = right[(star + 1)..].Trim();
                    if (!double.TryParse(probStr, out prob)) {
                        LogLineParseError($"Invalid probability '{probStr}'", probStr);
                        prob = 1.0;
                    }
                }

                rules.Add(new RulesModel(layerName, originSpecies, reactants, newSpec, prob, _currentRawLine, xMin, xMax, yMin, yMax));
            }
        }

        // apply settings
        int w = 0, h = 0;
        bool paused = false;
        var edgesMode = EdgeMode.BORDER;
        var gridTopology = GridTopology.RECT;
        if (settings.TryGetValue("WIDTH", out var ws))
			_=int.TryParse(ws, out w);
        if (settings.TryGetValue("HEIGHT", out var hs))
			_=int.TryParse(hs, out h);
        if (settings.TryGetValue("SHAPE", out var shape)) {
            if (!string.IsNullOrEmpty(shape) && string.Equals(shape.Trim(), "SPIRAL", StringComparison.OrdinalIgnoreCase)) {
                gridTopology = GridTopology.SPIRAL;
                Logger.Info("Using SPIRAL grid topology as per SHAPE setting.");
			}
        }
        if (settings.TryGetValue("PAUSE", out var ps))
            paused = ps != "0";
        if (settings.TryGetValue("EDGES", out var es)) {
            if (!Enum.TryParse<EdgeMode>(es.Trim(), true, out edgesMode)) {
                Logger.Warn($"Unknown EDGES value '{es}', defaulting to BORDER.");
            }
        }

        // produce final world file model with parsed data (file-loading WorldModel)
        var final = new WorldModel(
            width: w,
            height: h,
            paused: paused,
            species: species,
            layers: layers,
            rules: rules,
            edges: edgesMode,
            topology: gridTopology
        );

        return final;
    }
}
