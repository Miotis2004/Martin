# Diagnostics

This page is the Phase 16 alpha diagnostic catalog. It is generated from `Docs/diagnostics/catalog.json` and covers every `MRT####` code emitted by shipped source under `Martin/src`.

Each entry records severity, introduction phase, owning subsystem, stability status, message policy, explanation, triggering scenario, and remediation. Exact parameter values vary by source location; the compiler and tools remain authoritative for locations and formatted message arguments.

## Catalog integrity

- Machine-readable metadata: `Docs/diagnostics/catalog.json`.
- Validator: `scripts/validate-diagnostics.sh` or `scripts/validate-diagnostics.ps1`.
- Stable anchors use the lowercase diagnostic code, for example `#mrt2001`.

## build/code-generation

### MRT3001 — Build Code-Generation diagnostic MRT3001

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3001 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.CodeGeneration/CSharpEmitter.cs:20

### MRT3007 — Build Code-Generation diagnostic MRT3007

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3007 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:126

### MRT3008 — Build Code-Generation diagnostic MRT3008

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3008 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:268

### MRT3010 — Build Code-Generation diagnostic MRT3010

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3010 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Diagnostics/BuildDiagnostics.cs:11, Martin/src/Martin.Build/MartinBuildService.cs:263, Martin/src/Martin.Studio.Core/Services/StudioServices.cs:707

### MRT3011 — Build Code-Generation diagnostic MRT3011

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3011 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:113

### MRT3012 — Build Code-Generation diagnostic MRT3012

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3012 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.CodeGeneration/CSharpEmitter.cs:17, Martin/src/Martin.CodeGeneration/CSharpEmitter.cs:21

### MRT3014 — Build Code-Generation diagnostic MRT3014

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3014 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:268, Martin/src/Martin.CodeGeneration/CSharpEmitter.cs:18

### MRT3015 — Build Code-Generation diagnostic MRT3015

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3015 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:268

### MRT3016 — Build Code-Generation diagnostic MRT3016

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3016 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:268

### MRT3020 — Build Code-Generation diagnostic MRT3020

- **Severity:** error
- **Introduced:** Phase 3
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3020 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Diagnostics/GeneratedDiagnosticTranslator.cs:36

### MRT3100 — Build Code-Generation diagnostic MRT3100

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3100 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactCollector.cs:109

### MRT3101 — Build Code-Generation diagnostic MRT3101

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3101 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactCollector.cs:80

### MRT3110 — Build Code-Generation diagnostic MRT3110

- **Severity:** warning
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3110 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:333

### MRT3200 — Build Code-Generation diagnostic MRT3200

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3200 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:46

### MRT3201 — Build Code-Generation diagnostic MRT3201

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3201 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:54

### MRT3202 — Build Code-Generation diagnostic MRT3202

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3202 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:61

### MRT3203 — Build Code-Generation diagnostic MRT3203

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3203 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:63

### MRT3204 — Build Code-Generation diagnostic MRT3204

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3204 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:70

### MRT3205 — Build Code-Generation diagnostic MRT3205

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3205 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:72

### MRT3207 — Build Code-Generation diagnostic MRT3207

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3207 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:109

### MRT3208 — Build Code-Generation diagnostic MRT3208

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3208 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:119

### MRT3209 — Build Code-Generation diagnostic MRT3209

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3209 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:129

### MRT3210 — Build Code-Generation diagnostic MRT3210

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3210 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/MartinBuildService.cs:275, Martin/src/Martin.Build/MartinBuildService.cs:305

### MRT3220 — Build Code-Generation diagnostic MRT3220

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3220 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/RuntimeDiscovery.cs:76

### MRT3221 — Build Code-Generation diagnostic MRT3221

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3221 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/RuntimeDiscovery.cs:67

### MRT3222 — Build Code-Generation diagnostic MRT3222

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3222 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:88, Martin/src/Martin.Build/Artifacts/RuntimeDiscovery.cs:67

### MRT3223 — Build Code-Generation diagnostic MRT3223

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3223 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/RuntimeCompatibility.cs:22

### MRT3224 — Build Code-Generation diagnostic MRT3224

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3224 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/RuntimeCompatibility.cs:25

### MRT3225 — Build Code-Generation diagnostic MRT3225

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3225 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/RuntimeCompatibility.cs:19

### MRT3226 — Build Code-Generation diagnostic MRT3226

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT3226 is emitted by the build/code-generation subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/Artifacts/BuildArtifactValidator.cs:98

## cli/project-commands

### MRT4501 — Cli Project-Commands diagnostic MRT4501

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4501 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:74

### MRT4502 — Cli Project-Commands diagnostic MRT4502

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4502 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:74, Martin/src/Martin.Compiler.Cli/Program.cs:92, Martin/src/Martin.ProjectSystem/ProjectLocator.cs:11

### MRT4503 — Cli Project-Commands diagnostic MRT4503

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4503 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler.Cli/Program.cs:54, Martin/src/Martin.Compiler.Cli/Program.cs:55

### MRT4504 — Cli Project-Commands diagnostic MRT4504

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4504 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler.Cli/Program.cs:56

### MRT4505 — Cli Project-Commands diagnostic MRT4505

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4505 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/MartinProjectCreator.cs:50, Martin/src/Martin.ProjectSystem/MartinProjectCreator.cs:56, Martin/src/Martin.ProjectSystem/MartinProjectCreator.cs:78

### MRT4506 — Cli Project-Commands diagnostic MRT4506

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4506 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:74, Martin/src/Martin.ProjectSystem/MartinProjectCreator.cs:34, Martin/src/Martin.ProjectSystem/MartinProjectCreator.cs:44

### MRT4508 — Cli Project-Commands diagnostic MRT4508

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4508 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:74, Martin/src/Martin.Compiler.Cli/Program.cs:92

### MRT4509 — Cli Project-Commands diagnostic MRT4509

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4509 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler.Cli/Program.cs:57

### MRT4510 — Cli Project-Commands diagnostic MRT4510

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4510 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:60, Martin/src/Martin.Compiler.Cli/Program.cs:37

### MRT4511 — Cli Project-Commands diagnostic MRT4511

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4511 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/MartinProjectCreator.cs:91

### MRT4513 — Cli Project-Commands diagnostic MRT4513

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4513 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:307, Martin/src/Martin.ProjectSystem/ProjectCleaner.cs:37, Martin/src/Martin.ProjectSystem/ProjectCleaner.cs:46 ...

### MRT4514 — Cli Project-Commands diagnostic MRT4514

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4514 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ProjectCleaner.cs:93

### MRT4515 — Cli Project-Commands diagnostic MRT4515

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4515 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Build/BuildState/BuildStateStore.cs:116, Martin/src/Martin.Build/MartinBuildService.cs:250

### MRT4601 — Cli Project-Commands diagnostic MRT4601

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4601 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:281

### MRT4602 — Cli Project-Commands diagnostic MRT4602

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4602 is emitted by the cli/project-commands subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:277

## execution

### MRT4801 — Execution diagnostic MRT4801

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4801 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/MartinExecutionService.cs:50

### MRT4802 — Execution diagnostic MRT4802

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4802 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/MartinExecutionService.cs:64

### MRT4805 — Execution diagnostic MRT4805

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4805 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:255, Martin/src/Martin.Studio.Core/Services/StudioServices.cs:818

### MRT4806 — Execution diagnostic MRT4806

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4806 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/MartinExecutionService.cs:82

### MRT4807 — Execution diagnostic MRT4807

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4807 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:65, Martin/src/Martin.Compiler.Cli/Program.cs:42

### MRT4810 — Execution diagnostic MRT4810

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4810 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/MartinExecutionService.cs:486

### MRT4811 — Execution diagnostic MRT4811

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4811 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/MartinExecutionService.cs:157, Martin/src/Martin.Execution/MartinExecutionService.cs:254

### MRT4820 — Execution diagnostic MRT4820

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4820 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/WindowsExternalTerminalLauncher.cs:20

### MRT4821 — Execution diagnostic MRT4821

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4821 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/WindowsExternalTerminalLauncher.cs:34, Martin/src/Martin.Execution/WindowsExternalTerminalLauncher.cs:72

### MRT4822 — Execution diagnostic MRT4822

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4822 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/WindowsExternalTerminalLauncher.cs:44

### MRT4823 — Execution diagnostic MRT4823

- **Severity:** error
- **Introduced:** Phase 9
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4823 is emitted by the execution subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Execution/WindowsExternalTerminalLauncher.cs:58

## formatting

### MRT6451 — Formatting diagnostic MRT6451

- **Severity:** error
- **Introduced:** Phase 12
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT6451 is emitted by the formatting subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.LanguageServices/Features/Formatting/SafeFormattingService.cs:25

## lexer/parser

### MRT1001 — Lexer Parser diagnostic MRT1001

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1001 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:11

### MRT1002 — Lexer Parser diagnostic MRT1002

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1002 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:12

### MRT1003 — Lexer Parser diagnostic MRT1003

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1003 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:13

### MRT1004 — Lexer Parser diagnostic MRT1004

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1004 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:14

### MRT1005 — Lexer Parser diagnostic MRT1005

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1005 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:15

### MRT1101 — Lexer Parser diagnostic MRT1101

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1101 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:16

### MRT1102 — Lexer Parser diagnostic MRT1102

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1102 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:17

### MRT1107 — Lexer Parser diagnostic MRT1107

- **Severity:** error
- **Introduced:** Phase 1
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT1107 is emitted by the lexer/parser subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Diagnostics/Diagnostics.cs:18

## project-system

### MRT4001 — Project-System diagnostic MRT4001

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4001 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:218, Martin/src/Martin.ProjectSystem/ProjectLocator.cs:40, Martin/src/Martin.ProjectSystem/ProjectLocator.cs:51

### MRT4002 — Project-System diagnostic MRT4002

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4002 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:197, Martin/src/Martin.ProjectSystem/ManifestParser.cs:82

### MRT4003 — Project-System diagnostic MRT4003

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4003 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:146, Martin/src/Martin.ProjectSystem/ManifestParser.cs:153, Martin/src/Martin.ProjectSystem/ManifestParser.cs:162

### MRT4004 — Project-System diagnostic MRT4004

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4004 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:139, Martin/src/Martin.ProjectSystem/ManifestParser.cs:140

### MRT4005 — Project-System diagnostic MRT4005

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4005 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:41

### MRT4006 — Project-System diagnostic MRT4006

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4006 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ProjectSystem.cs:47

### MRT4007 — Project-System diagnostic MRT4007

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4007 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:131, Martin/src/Martin.ProjectSystem/SourceDiscovery.cs:33

### MRT4008 — Project-System diagnostic MRT4008

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4008 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:129

### MRT4009 — Project-System diagnostic MRT4009

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4009 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:155, Martin/src/Martin.ProjectSystem/ManifestParser.cs:164, Martin/src/Martin.ProjectSystem/ManifestParser.cs:173 ...

### MRT4010 — Project-System diagnostic MRT4010

- **Severity:** warning
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4010 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:102, Martin/src/Martin.ProjectSystem/ManifestParser.cs:108, Martin/src/Martin.ProjectSystem/ManifestParser.cs:113

### MRT4011 — Project-System diagnostic MRT4011

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4011 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:120

### MRT4013 — Project-System diagnostic MRT4013

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4013 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:136

### MRT4014 — Project-System diagnostic MRT4014

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4014 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:137

### MRT4015 — Project-System diagnostic MRT4015

- **Severity:** error
- **Introduced:** Phase 4
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4015 is emitted by the project-system subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.ProjectSystem/ManifestParser.cs:138

## semantic

### MRT2001 — Semantic diagnostic MRT2001

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2001 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:171, Martin/src/Martin.Compiler/Binding/Binder.cs:172, Martin/src/Martin.Compiler/Binding/Binder.cs:232 ...

### MRT2002 — Semantic diagnostic MRT2002

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2002 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:112

### MRT2003 — Semantic diagnostic MRT2003

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2003 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:160

### MRT2004 — Semantic diagnostic MRT2004

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2004 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:172

### MRT2005 — Semantic diagnostic MRT2005

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2005 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:175

### MRT2006 — Semantic diagnostic MRT2006

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2006 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:175

### MRT2007 — Semantic diagnostic MRT2007

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2007 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:283

### MRT2010 — Semantic diagnostic MRT2010

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2010 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:158

### MRT2011 — Semantic diagnostic MRT2011

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2011 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:158

### MRT2012 — Semantic diagnostic MRT2012

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2012 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:158

### MRT2014 — Semantic diagnostic MRT2014

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2014 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:13, Martin/src/Martin.Compiler/Compilation.cs:47, Martin/src/Martin.LanguageServices/Features/Classification/SemanticClassificationProvider.cs:89

### MRT2015 — Semantic diagnostic MRT2015

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2015 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:112

### MRT2016 — Semantic diagnostic MRT2016

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2016 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:112

### MRT2017 — Semantic diagnostic MRT2017

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2017 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:14, Martin/src/Martin.Compiler/Compilation.cs:15

### MRT2018 — Semantic diagnostic MRT2018

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2018 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:55

### MRT2019 — Semantic diagnostic MRT2019

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2019 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:50

### MRT2022 — Semantic diagnostic MRT2022

- **Severity:** error
- **Introduced:** Phase 2
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2022 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:160

### MRT2100 — Semantic diagnostic MRT2100

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2100 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:13

### MRT2101 — Semantic diagnostic MRT2101

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2101 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:48

### MRT2102 — Semantic diagnostic MRT2102

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2102 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:173, Martin/src/Martin.Compiler/Binding/Binder.cs:174, Martin/src/Martin.Compiler/Binding/Binder.cs:206 ...

### MRT2103 — Semantic diagnostic MRT2103

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2103 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:105

### MRT2104 — Semantic diagnostic MRT2104

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2104 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:172, Martin/src/Martin.Compiler/Binding/Binder.cs:174

### MRT2105 — Semantic diagnostic MRT2105

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2105 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:174

### MRT2106 — Semantic diagnostic MRT2106

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2106 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:158

### MRT2108 — Semantic diagnostic MRT2108

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2108 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:105

### MRT2109 — Semantic diagnostic MRT2109

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2109 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:218

### MRT2110 — Semantic diagnostic MRT2110

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2110 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:48

### MRT2111 — Semantic diagnostic MRT2111

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2111 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:254

### MRT2122 — Semantic diagnostic MRT2122

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2122 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:113

### MRT2123 — Semantic diagnostic MRT2123

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2123 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:112

### MRT2124 — Semantic diagnostic MRT2124

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2124 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:47

### MRT2126 — Semantic diagnostic MRT2126

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2126 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:173

### MRT2127 — Semantic diagnostic MRT2127

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2127 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:113

### MRT2140 — Semantic diagnostic MRT2140

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2140 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:150

### MRT2141 — Semantic diagnostic MRT2141

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2141 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:149

### MRT2142 — Semantic diagnostic MRT2142

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2142 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:180

### MRT2144 — Semantic diagnostic MRT2144

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2144 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:48

### MRT2145 — Semantic diagnostic MRT2145

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2145 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:172, Martin/src/Martin.LanguageServices/Features/Classification/SemanticClassificationProvider.cs:89

### MRT2157 — Semantic diagnostic MRT2157

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2157 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:53

### MRT2161 — Semantic diagnostic MRT2161

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2161 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:42

### MRT2162 — Semantic diagnostic MRT2162

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2162 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2163 — Semantic diagnostic MRT2163

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2163 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:42

### MRT2164 — Semantic diagnostic MRT2164

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2164 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:42

### MRT2165 — Semantic diagnostic MRT2165

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2165 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2166 — Semantic diagnostic MRT2166

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2166 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2167 — Semantic diagnostic MRT2167

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2167 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:13, Martin/src/Martin.Compiler/Compilation.cs:47

### MRT2170 — Semantic diagnostic MRT2170

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2170 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:48

### MRT2171 — Semantic diagnostic MRT2171

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2171 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2172 — Semantic diagnostic MRT2172

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2172 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2173 — Semantic diagnostic MRT2173

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2173 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2174 — Semantic diagnostic MRT2174

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2174 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2175 — Semantic diagnostic MRT2175

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2175 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2176 — Semantic diagnostic MRT2176

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2176 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:43

### MRT2177 — Semantic diagnostic MRT2177

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2177 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:42

### MRT2179 — Semantic diagnostic MRT2179

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2179 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:48

### MRT2180 — Semantic diagnostic MRT2180

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2180 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Generics/GenericTypeBinding.cs:49

### MRT2183 — Semantic diagnostic MRT2183

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2183 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:39

### MRT2185 — Semantic diagnostic MRT2185

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2185 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Generics/GenericTypeBinding.cs:36, Martin/src/Martin.LanguageServices/Features/Classification/SemanticClassificationProvider.cs:89

### MRT2189 — Semantic diagnostic MRT2189

- **Severity:** error
- **Introduced:** Phase 7
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2189 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:40

### MRT2190 — Semantic diagnostic MRT2190

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2190 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:245

### MRT2191 — Semantic diagnostic MRT2191

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2191 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:38

### MRT2192 — Semantic diagnostic MRT2192

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2192 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:59, Martin/src/Martin.Compiler/Compilation.cs:73, Martin/src/Martin.Compiler/Compilation.cs:77

### MRT2193 — Semantic diagnostic MRT2193

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2193 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:86

### MRT2194 — Semantic diagnostic MRT2194

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2194 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:51

### MRT2195 — Semantic diagnostic MRT2195

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2195 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:25

### MRT2196 — Semantic diagnostic MRT2196

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2196 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:165

### MRT2197 — Semantic diagnostic MRT2197

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2197 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:146

### MRT2198 — Semantic diagnostic MRT2198

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2198 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:84

### MRT2199 — Semantic diagnostic MRT2199

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2199 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:71, Martin/src/Martin.Compiler/Compilation.cs:79

### MRT2201 — Semantic diagnostic MRT2201

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2201 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:86

### MRT2202 — Semantic diagnostic MRT2202

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2202 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:98

### MRT2203 — Semantic diagnostic MRT2203

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2203 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:112

### MRT2204 — Semantic diagnostic MRT2204

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2204 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:123

### MRT2205 — Semantic diagnostic MRT2205

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2205 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:158

### MRT2206 — Semantic diagnostic MRT2206

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2206 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Patterns/PatternBinder.cs:164

### MRT2207 — Semantic diagnostic MRT2207

- **Severity:** error
- **Introduced:** Phase 13
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2207 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:117

### MRT2304 — Semantic diagnostic MRT2304

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2304 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Compilation.cs:24, Martin/src/Martin.Compiler/Compilation.cs:32

### MRT2306 — Semantic diagnostic MRT2306

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2306 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:305

### MRT2307 — Semantic diagnostic MRT2307

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2307 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:305

### MRT2308 — Semantic diagnostic MRT2308

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2308 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:308, Martin/src/Martin.Compiler/Binding/Binder.cs:314

### MRT2309 — Semantic diagnostic MRT2309

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2309 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:228, Martin/src/Martin.Compiler/Binding/Binder.cs:309, Martin/src/Martin.Compiler/Binding/Binder.cs:315

### MRT2310 — Semantic diagnostic MRT2310

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2310 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:275

### MRT2311 — Semantic diagnostic MRT2311

- **Severity:** error
- **Introduced:** Phase 14
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2311 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:264

### MRT2401 — Semantic diagnostic MRT2401

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2401 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:57

### MRT2402 — Semantic diagnostic MRT2402

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2402 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:166, Martin/src/Martin.Compiler/Binding/Binder.cs:55, Martin/src/Martin.Compiler/Compilation.cs:69

### MRT2403 — Semantic diagnostic MRT2403

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2403 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:72

### MRT2406 — Semantic diagnostic MRT2406

- **Severity:** error
- **Introduced:** Phase 15
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT2406 is emitted by the semantic subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Compiler/Binding/Binder.cs:80

## studio

### MRT4901 — Studio diagnostic MRT4901

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT4901 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Cli/CommandHandlers.cs:330

### MRT5001 — Studio diagnostic MRT5001

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5001 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:661, Martin/src/Martin.Studio.Core/Services/StudioServices.cs:718

### MRT5002 — Studio diagnostic MRT5002

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5002 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Models/StudioModels.cs:98, Martin/src/Martin.Studio.Core/Services/StudioServices.cs:662, Martin/src/Martin.Studio.Core/Services/StudioServices.cs:719 ...

### MRT5003 — Studio diagnostic MRT5003

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5003 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:508

### MRT5009 — Studio diagnostic MRT5009

- **Severity:** warning
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5009 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:106

### MRT5010 — Studio diagnostic MRT5010

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5010 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:678

### MRT5011 — Studio diagnostic MRT5011

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5011 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:872

### MRT5012 — Studio diagnostic MRT5012

- **Severity:** warning
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5012 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:277

### MRT5013 — Studio diagnostic MRT5013

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5013 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:849

### MRT5014 — Studio diagnostic MRT5014

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5014 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:880

### MRT5101 — Studio diagnostic MRT5101

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5101 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:779

### MRT5102 — Studio diagnostic MRT5102

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5102 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:780, Martin/src/Martin.Studio.Core/Services/StudioServices.cs:801

### MRT5103 — Studio diagnostic MRT5103

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5103 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:794

### MRT5104 — Studio diagnostic MRT5104

- **Severity:** error
- **Introduced:** Phase 5
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5104 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:781

### MRT5201 — Studio diagnostic MRT5201

- **Severity:** error
- **Introduced:** Phase 11
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5201 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.WinUI/App.xaml.cs:156

### MRT5203 — Studio diagnostic MRT5203

- **Severity:** warning
- **Introduced:** Phase 11
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5203 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.WinUI/Services/MonacoEditorHost.cs:294, Martin/src/Martin.Studio.WinUI/Services/MonacoEditorHost.cs:347

### MRT5205 — Studio diagnostic MRT5205

- **Severity:** warning
- **Introduced:** Phase 11
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5205 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:107

### MRT5301 — Studio diagnostic MRT5301

- **Severity:** warning
- **Introduced:** Phase 11
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5301 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:104

### MRT5303 — Studio diagnostic MRT5303

- **Severity:** warning
- **Introduced:** Phase 11
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5303 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:105

### MRT5404 — Studio diagnostic MRT5404

- **Severity:** error
- **Introduced:** Phase 11
- **Status:** active
- **Message form:** See shipped source for the exact parameterized message form; catalog validation keeps this code present and unique.
- **Explanation:** MRT5404 is emitted by the studio subsystem when the documented alpha implementation rejects or warns about the triggering condition.
- **Cause:** The program, manifest, command, build artifact, runtime environment, editor state, or formatting input violated the alpha rule associated with this diagnostic code.
- **Invalid example / triggering scenario:** Triggering scenario: use the malformed source, manifest, command option, stale artifact, missing file, unsupported runtime, or editor state described by the title and source message.
- **Correction / remediation:** Remediation: correct the source or manifest, choose a supported option/runtime/artifact, save or reopen the project as needed, then rerun the command.
- **Source inventory:** Martin/src/Martin.Studio.Core/Services/StudioServices.cs:108
