using System.ComponentModel.DataAnnotations;

namespace DocGen.Services;

// Validation attributes whose messages follow the current language.

public class LRequiredAttribute : RequiredAttribute
{
    public override string FormatErrorMessage(string name) => Loc.Text("val.required");
}

public class LStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    public override string FormatErrorMessage(string name) =>
        string.Format(Loc.Text("val.length"), MinimumLength, MaximumLength);
}

public class LCompareAttribute(string otherProperty) : CompareAttribute(otherProperty)
{
    public override string FormatErrorMessage(string name) => Loc.Text("val.match");
}
