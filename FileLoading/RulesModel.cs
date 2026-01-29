using Biome2.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Biome2.FileLoading;

public class RulesModel {
    // Names are stored (layer and species names) so the loader can map to indices later
    public string LayerName { get; }
    public string OriginSpeciesName { get; }
    public ReactantModel[] Reactants { get; }
    public string NewSpeciesName { get; }
    public double Probability { get; }

    // Resolved indices populated by validation step. -1 when unresolved.
    public int ResolvedLayerIndex { get; set; } = -1;
    public int ResolvedOriginSpeciesIndex { get; set; } = -1;
    public int ResolvedNewSpeciesIndex { get; set; } = -1;

    // Logging purposes
    private uint _ruleOperationCount; // count the number of times this rule has executed
    private string _verboseRule = string.Empty; // verbose description of the rule, basically the pre-parsed line, possibly the line number as well in the file

    public RulesModel(
        string layerName,
        string originSpeciesName,
        ReactantModel[] reactants,
        string newSpeciesName,
        double probability,
        string verboseRule
    ) {
        LayerName = layerName ?? string.Empty;
        OriginSpeciesName = originSpeciesName ?? string.Empty;
        Reactants = reactants ?? Array.Empty<ReactantModel>();
        NewSpeciesName = newSpeciesName ?? string.Empty;
        Probability = probability;
        _verboseRule = verboseRule ?? string.Empty;
        _ruleOperationCount = 0;
    }

    public void IncrementOpCount() {
        _ruleOperationCount++;
	}

    public void ReportRuleDetails() {
		// Left side: optional layer, origin, and reactants
		string left;
		if (!string.IsNullOrEmpty(LayerName))
			left = $"{LayerName}:{OriginSpeciesName}";
		else
			left = OriginSpeciesName;

		if (Reactants != null && Reactants.Length > 0) {
			var parts = new List<string>();
			foreach (var react in Reactants) {
				var sb = string.Empty;
				if (react.Count > 0)
					sb += react.Count.ToString(CultureInfo.InvariantCulture);
				if (!string.IsNullOrEmpty(react.LayerName))
					sb += react.LayerName + ":";
				sb += react.SpeciesName;
				if (react.Sign == 1)
					sb += "+";
				else if (react.Sign == -1)
					sb += "-";
				parts.Add(sb);
			}
			left += " + " + string.Join(' ', parts);
		}

		// Right side: new species and optional probability
		string right = NewSpeciesName ?? string.Empty;
		if (Probability < 1.0)
			right += "*" + Probability.ToString(CultureInfo.InvariantCulture);

		// operation count 
		var opCount = _ruleOperationCount;

		Logger.Info($"{left} -> {right}\t\t - operation count: {opCount}");
	}
}
