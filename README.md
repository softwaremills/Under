# Under - Identifier Replacement

*Anthony Mills*

This tool replaces identifiers globally across source files with smaller versions. Usually the identifiers in question are CSS and HTML IDs and classes and public JavaScript identifiers. The result is more obfuscated source code as well as code that compresses better; usually compressed replaced minified code will be about 10% smaller than compressed minified code.

## The Problem

JavaScript minifiers work great on local variables, because they can guarantee correctness.

But what about public identifiers? There, you can't do as much, because the minifier can't survey the whole space. It doesn't know your intentions, and it doesn't process CSS files or anything other than JavaScript.

What if you had something that could survey the whole space?

Something that could look at all your HTML files, all your CSS files, and all your JS files, then do global replacements with shorter identifiers?

## The Solution

Name things you want replaced with_ ending_ underscores_. This has to be done manually, of course.

Name things you don't want replaced without them.

When ready to deploy, run Under on your deployment folder, telling it masks matching all the files you want to do replacements on.

## Examples of Things You Can Replace

As long as you name things consistently across files, replacing identifiers works great.

`<div id="htmlIdentifiers_"></div>  <!-- in HTML files -->`

`<div class="cssClasses_"></div>`

`#htmlIdentifiers_ { color: #333 }  /* in CSS files */`

`.cssClasses_ { margin: 15px 20px }`

`$("#htmlIdentifiers_, .cssClasses_").show(); // in JavaScript files`

`return { public_: "identifiers", work_: "well" };`

## Not For Newbies

To use this tool properly, you need to know what a public identifier is in JavaScript. If you don't know that, this tool might not be for you.

## Things to Watch Out For

If you build HTML using compiled code, don't target those identifiers for replacements. So if you have a CSS class mentioned in compiled code, for instance, make sure it has a name that doesn't end in an underline.

If you build strings (for instance, if you're concatenating strings to make IDs for ASP.NET applications to make an identifier like `Employees_0__FirstName`, you might have code that concatenates like `"Employees_" + index + "__FirstName"`. In this case, Under will wrongly detect `Employees_` as an identifier targeted for replacement.

To try to combat this, Under only replaces identifiers with a single ending underscore. So something like `__python_directives__` won't trigger replacement.

## Frequently Asked Questions

**What if it replaces underscored identifiers with identifiers that already exist?**

Under is careful to not do that. Replacements are guaranteed to be words not mentioned elsewhere in your files.

**How about reserved words?**

Under won't use any words reserved by JavaScript.

**Why would you want to specify letters to make identifiers with?**

Because it's fun. You can make your source code have a bunch of variables that look like your name, or your company, or your product. So if you use "-l bB,oO0,sS5,tT,oO0,nN" then you'll get replaced variable names that look like *b0sT*, *Bo5tONb0*, *B0StoN*, etc.

If you let the program pick letters, it will look at the other content of the file and pick letters that already exist in descending order of frequency. Choosing this way helps the resulting file compress better; sometimes, having slightly larger uncompressed sizes allows you to have slightly smaller compressed sizes.

**What about character encodings?**

Under reads source code according to the hints in the files, generally either looking for Unicode byte order marks or interpreting source as UTF-8. It will rewrite files if it does any replacements in them. If it rewrites files, it writes them out as UTF-8 with a byte order mark.